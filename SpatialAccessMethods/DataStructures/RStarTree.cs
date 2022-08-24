using Garyon.Extensions;
using Garyon.Objects;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using System.Diagnostics;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.DataStructures;

public sealed class RStarTree<TValue> : ISecondaryStorageDataStructure
    where TValue : ILocated, IID, IRecordSerializable<TValue>
{
    private const int treeNodeSize = 256;
    
    private readonly SerialIDHandler serialIDHandler; 
    private Node? root;

    public int Order { get; }

    public bool IsEmpty => root is null;

    ChildBufferController ISecondaryStorageDataStructure.BufferController => TreeBufferController;

    public RStarTreeBufferController TreeBufferController { get; }
    public RecordEntryBufferController RecordBufferController { get; }

    public int Dimensionality => RecordBufferController.HeaderBlock.Dimensionality;

    private int nodeCount;

    public int NodeCount
    {
        get => nodeCount;
        private set
        {
            if (nodeCount == value)
                return;
            
            nodeCount = value;
            RecordBufferController.HeaderBlock.TreeNodeCount = value;

            TreeBufferController.ResizeForEntryCount(value);
        }
    }

    // Always strive for >= 50% capacity
    public int MinChildren => (Order + 1) / 2;
    public int MaxChildren => Order;

    public RStarTree(int order, ChildBufferController treeBufferController, RecordEntryBufferController recordBufferController, MinHeap<int> idGapHeap)
    {
        Order = order;
        TreeBufferController = new(this, treeBufferController);
        RecordBufferController = recordBufferController;

        nodeCount = RecordBufferController.HeaderBlock.TreeNodeCount;

        serialIDHandler = new(idGapHeap)
        {
            MaxIDAccessors = new(GetMaxID, SetMaxID),
            EntryCountGetter = GetEntryCount,
        };
    }

    private int GetMaxID() => RecordBufferController.HeaderBlock.MaxTreeNodeID;
    private void SetMaxID(int maxID) => RecordBufferController.HeaderBlock.MaxTreeNodeID = maxID;
    private int GetEntryCount() => RecordBufferController.HeaderBlock.TreeNodeCount;

    public LeafNode? LeafContainingValue(TValue value)
    {
        var leaf = LeafContaining(value.Location);
        if (leaf is null)
            return null;

        // Further evaluate that the location is indeed contained in the leaf
        var leafValue = leaf.ValueAt(value.Location);
        if (leafValue?.IsValid != true)
            return null;

        return leaf;
    }
    public LeafNode? LeafContaining(Point point)
    {
        var current = root;
        while (current is not null)
        {
            if (current is LeafNode leaf)
                return leaf;

            var currentParent = current as ParentNode;
            var child = currentParent!.ChildContaining(point);
            current = child;
        }

        return null;
    }
    public TValue? GetValue(Point point)
    {
        var leaf = LeafContaining(point);
        if (leaf is null)
            return default;

        return leaf.ValueAt(point);
    }

    public void Clear()
    {
        root = null;
        NodeCount = 0;
        serialIDHandler.Clear();
    }

    private LeafNode ChooseLeaf(Point point)
    {
        var leaf = LeafContaining(point);
        if (leaf is not null)
            return leaf;

        return null;
    }

    private LeafNode ChooseSubtree(TValue entry)
    {
        var node = root;

        while (true)
        {
            if (node is LeafNode leaf)
                return leaf;

            var parentNode = node as ParentNode;
            var children = parentNode!.GetChildren().ToArray();

            var comparer = BestSubtreeInfo.IPreferenceScoreComparer.CreateForNodeType(children.First().NodeType);
            
            var bestSubtree = new BestSubtreeInfo(entry, comparer);
            bestSubtree.RegisterNodes(children);
            node = bestSubtree.BestNode;
        }
    }

    private void Split(Node node)
    {
        var d30 = AttemptSplit(0.30);
        var d40 = AttemptSplit(0.40);
        var splitDistribution = d40;

        if (d30.OverlapValue < d40.OverlapValue)
            splitDistribution = d30;

        {
            var newCurrentRegion = splitDistribution.LeftMBR;
            // We don't care about the right MBR
        }
        // TODO: Create the new split nodes

        RectangleDistribution AttemptSplit(double maxOrderRate)
        {
            var axis = ChooseSplitAxis(maxOrderRate, out var rectangleDistributions);
            return ChooseSplitIndex(rectangleDistributions);
        }

        Axis ChooseSplitAxis(double maxOrderRate, out RectangleDistribution[] rectangleDistributions)
        {
            // This should be faster than invoking Dimensionality
            int dimensions = node.Region.Rank;
            var childrenRectangles = node.GetChildrenRectangles().ToArray();
            int fixedEntries = FixedEntries(maxOrderRate);
            int distributionCount = DistributionCount(maxOrderRate);
            var bestDistributions = new AxisDistributionResult(Axis.Invalid, null!);
            for (int i = 0; i < dimensions; i++)
            {
                var sorted = childrenRectangles.ToArray().SortBy(RectangleComparer);
                var distributions = GetDistributions();
                var currentDistributionsResult = new AxisDistributionResult((Axis)i, distributions);
                if (currentDistributionsResult.MarginValue < bestDistributions.MarginValue)
                    bestDistributions = currentDistributionsResult;

                RectangleDistribution[] GetDistributions()
                {
                    var result = new RectangleDistribution[distributionCount];

                    for (int k = 1; k < distributionCount; k++)
                    {
                        int split = fixedEntries - 1 + k;
                        result[k - 1] = new(split, sorted);
                    }

                    return result;
                }
                int RectangleComparer(Rectangle a, Rectangle b)
                {
                    int comparison = a.MinPoint.GetCoordinate(i).CompareTo(b.MinPoint.GetCoordinate(i));
                    if (comparison is not 0)
                        return comparison;

                    return a.MaxPoint.GetCoordinate(i).CompareTo(b.MaxPoint.GetCoordinate(i));
                }
            }

            rectangleDistributions = bestDistributions.Distributions;
            return bestDistributions.Axis;
        }
        RectangleDistribution ChooseSplitIndex(RectangleDistribution[] rectangleDistributions)
        {
            return rectangleDistributions.Min(new RectangleDistribution.Comparer())!;
        }
    }
    private int DistributionCount(double maxOrderRate)
    {
        int entries = FixedEntries(maxOrderRate);
        return Order - 2 * (entries + 1);
    }
    private int FixedEntries(double maxOrderRate)
    {
        return (int)Math.Floor(maxOrderRate * Order);
    }

    private enum Axis
    {
        Invalid = -1,
        X,
        Y,
        Z,
        W,
    }

    private record AxisDistributionResult(Axis Axis, RectangleDistribution[] Distributions)
    {
        public double MarginValue { get; } = Distributions?.Sum(d => d.MarginValue) ?? double.MaxValue;
    }

    private record RectangleDistribution(int SplitIndex, Rectangle[] Rectangles)
    {
        public Span<Rectangle> Left => Rectangles.AsSpan()[..SplitIndex];
        public Span<Rectangle> Right => Rectangles.AsSpan()[SplitIndex..];

        public IEnumerable<Rectangle> LeftEnumerable => Rectangles.Take(SplitIndex);
        public IEnumerable<Rectangle> RightEnumerable => Rectangles.Skip(SplitIndex);

        public Rectangle LeftMBR => Rectangle.CreateForRectangles(LeftEnumerable.ToArray());
        public Rectangle RightMBR => Rectangle.CreateForRectangles(RightEnumerable.ToArray());

        public double AreaValue => RStarTree<TValue>.AreaValue(LeftMBR, RightMBR);
        public double MarginValue => RStarTree<TValue>.MarginValue(LeftMBR, RightMBR);
        public double OverlapValue => RStarTree<TValue>.OverlapValue(LeftMBR, RightMBR);

        public sealed class Comparer : IComparer<RectangleDistribution>
        {
#nullable disable
            public int Compare(RectangleDistribution a, RectangleDistribution b)
            {
                int comparison = a.OverlapValue.CompareTo(b.OverlapValue);
                if (comparison is not 0)
                    return comparison;

                return a.AreaValue.CompareTo(b.AreaValue);
            }
#nullable restore
        }
    }

#nullable disable
    private class BestSubtreeInfo
    {
        private readonly IPreferenceScoreComparer preferenceScoreComparer;
        
        public TValue Entry { get; }
        
        public PreferenceScore Best { get; private set; }

        public Node BestNode => Best.Node;

        public BestSubtreeInfo(TValue entry, IPreferenceScoreComparer comparer)
        {
            Entry = entry;
            preferenceScoreComparer = comparer;
        }

        public void RegisterNodes(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
                RegisterNode(node);
        }

        public void RegisterNode(Node node)
        {
            var preferenceScore = new PreferenceScore(node, Entry);

            if (BestNode is null)
            {
                Best = preferenceScore;
                return;
            }

            int comparison = preferenceScoreComparer.Compare(Best, preferenceScore);
            // Best better than the new pref score
            if (comparison <= 0)
                return;

            Best = preferenceScore;
        }

        public struct LeafChildrenComparer : IPreferenceScoreComparer
        {
            public int Compare(PreferenceScore left, PreferenceScore right)
            {
                int comparison = left.OverlapEnlargement.CompareTo(right.OverlapEnlargement);
                if (comparison is not 0)
                    return comparison;

                return IPreferenceScoreComparer.CompareAreaAndBelow(left, right);
            }
        }
        public struct ParentChildrenComparer : IPreferenceScoreComparer
        {
            public int Compare(PreferenceScore left, PreferenceScore right)
            {
                return IPreferenceScoreComparer.CompareAreaAndBelow(left, right);
            }
        }

        public interface IPreferenceScoreComparer : IComparer<PreferenceScore> 
        {
            protected static int CompareAreaAndBelow(PreferenceScore left, PreferenceScore right)
            {
                int comparison = left.AreaEnlargement.CompareTo(right.AreaEnlargement);
                if (comparison is not 0)
                    return comparison;

                return left.Area.CompareTo(right.Area);
            }

            public static IPreferenceScoreComparer CreateForNodeType(NodeType type) => type switch
            {
                NodeType.Leaf => new LeafChildrenComparer(),
                NodeType.Parent => new ParentChildrenComparer(),

                _ => null!,
            };
        }

        public struct PreferenceScore
        {
            public Node Node { get; }
            
            public double OverlapEnlargement { get; private set; }
            public double AreaEnlargement { get; private set; }
            public double Area { get; private set; }

            public PreferenceScore(Node node, TValue entry)
            {
                Node = node;
                Populate(entry);
            }

            private void Populate(TValue entry)
            {
                var parent = Node.GetParent();

                var newRegion = Node.Region.Expand(entry.Location);
                double previousOverlap = parent.GetTotalChildrenOverlappingArea(Node);
                double newOverlap = parent.GetTotalChildrenOverlappingArea(Node, newRegion);
                OverlapEnlargement = newOverlap - previousOverlap;

                double oldArea = Node.Region.Area;
                double newArea = newRegion.Area;
                AreaEnlargement = newArea - oldArea;
                Area = newArea;
            }
        }
    }
#nullable enable

    private static double OverlapValue(Rectangle a, Rectangle b)
    {
        return a.OverlappingArea(b);
    }
    private static double AreaValue(Rectangle a, Rectangle b)
    {
        return a.Area + b.Area;
    }
    private static double MarginValue(Rectangle a, Rectangle b)
    {
        return a.Margin + b.Margin;
    }

    public void Insert(TValue value)
    {
        if (root is null)
            InitializeRoot(value);

        var leaf = ChooseLeaf(value.Location);

        // First handle the case where the leaf is full
        if (leaf.IsFull)
        {
            // Split accordingly
        }
        else
        {
            // Ensure the leaf contains
            leaf.EnsureRegionContains(value.Location);
        }
        // TODO
    }

    public bool Remove(Point point)
    {
        var leaf = LeafContaining(point);
        if (leaf is null)
            return false;

        // TODO
        return true;

    }
    public bool Remove(TValue value)
    {
        return Remove(value.Location);
    }

    public int GetHeightForEntryCount(int entryCount)
    {
        return (int)Math.Ceiling(Math.Log(entryCount, Order));
    }

    public void BulkLoad(IEnumerable<TValue> entries)
    {
        if (!IsEmpty)
            throw new InvalidOperationException("Cannot bulk load an R*-tree that already contains nodes.");

        var entryArray = entries.ToExistingOrNewArray();
        // Reset the ID as the value is not passed through in the case of structs
        for (int i = 0; i < entryArray.Length; i++)
            entryArray[i].ID = i;

        int nodeCount = (int)Math.Ceiling((double)entryArray.Length / Order);
        TreeBufferController.EnsureLengthForEntry(nodeCount);
        
        int dimensionality = RecordBufferController.HeaderBlock.Dimensionality;
        int treeHeight = GetHeightForEntryCount(entryArray.Length);
        int slabCount = (int)Math.Ceiling(Math.Pow(nodeCount, 1D / dimensionality));

        StableSortEntryArray(entryArray, 0);
        var childrenDesignNodes = CreateDesignNodes(entryArray, slabCount, NodeID.Root);

        // TODO: Something goes horribly wrong and doesn't account for the order of the tree

        var rootChildrenIDs = childrenDesignNodes.Select(node => node.ID);
        var rootRectangle = Rectangle.CreateForPoints(entryArray.Select(entry => entry.Location).ToArray());
        root = new ParentNode(this, NodeID.Root, NodeID.Null, rootChildrenIDs, rootRectangle);

        BulkLoadSlab(childrenDesignNodes, BulkLoadArguments.Initial(dimensionality, treeHeight, slabCount));

        NodeCount = entryArray.Length;
    }

    private static TValue[] StableSortEntryArray(TValue[] entryArray, int coordinate)
    {
        var comparer = new Point.CoordinateComparer(coordinate);
        return entryArray.SortBy(new ILocated.LocationComparer<TValue, Point.CoordinateComparer>(comparer));
    }

    private void BulkLoadSlab(IEnumerable<BulkDesignNode> designNodes, BulkLoadArguments arguments)
    {
        var designNodeArray = designNodes.ToArray();
        int rectangleCount = designNodeArray.Length;

        if (arguments.HasReachedLeafLevel)
        {
            // Finalize the design nodes into leaf nodes
            var nodes = designNodeArray.Select(node => node.FinalizeAsLeafNode(this)).ToArray();
            foreach (var node in nodes)
            {
                // Explicitly write the changes to the buffer, for the nodes will not be further processed
                node.WriteUponDestruction = false;
                node.WriteChangesToBuffer();
            }

            return;
        }

        var nextArguments = arguments.NextIteration();
        foreach (var designNode in designNodeArray)
        {
            var entryArray = designNode.EntryIDs.Select(GetEntry).ToArray();
            StableSortEntryArray(entryArray, arguments.CurrentDimension);
            var childrenDesignNodes = CreateDesignNodes(entryArray, arguments.SlabCount, designNode.ID);
            var finalized = designNode.FinalizeAsParentNode(this, childrenDesignNodes);

            // Then, for each formed slab, create a parent node representing it that will eventually contain nodes
            BulkLoadSlab(childrenDesignNodes, nextArguments);
        }
    }

    private record struct BulkLoadArguments(int CurrentDimension, int Dimensionality, int TreeHeight, int SlabCount, int CurrentDepth)
    {
        public bool HasReachedLeafLevel => CurrentDepth >= TreeHeight - 1;

        public BulkLoadArguments NextIteration()
        {
            return this with
            {
                CurrentDimension = (CurrentDimension + 1) % Dimensionality,
                CurrentDepth = CurrentDepth + 1,
            };
        }
        
        public static BulkLoadArguments Initial(int dimensionality, int treeHeight, int slabCount)
        {
            return new(0, dimensionality, treeHeight, slabCount, 0);
        }
    }

    private ICollection<BulkDesignNode> CreateDesignNodes(TValue[] entryArray, int slabCount, int parentID)
    {
        var chunks = entryArray.ToChunks(slabCount).ToArray();

        var childrenDesignNodes = new List<BulkDesignNode>();
        foreach (var chunk in chunks)
        {
            childrenDesignNodes.Add(new(AllocateNextID(), parentID, chunk));
        }

        return childrenDesignNodes;
    }

    public IEnumerable<TValue> SkylineQuery(Extremum dominatingExtremum)
    {
        if (IsEmpty)
            return Enumerable.Empty<TValue>();

        var subordinateExtremum = dominatingExtremum.Opposing();
        var origin = root!.Region.ExtremumPoint(dominatingExtremum);

        var nodeQueue = new PriorityQueue<Monad<Node, TValue>, double>();

        var result = new List<TValue>();

        while (nodeQueue.Count > 0)        
        {
            var dequeued = nodeQueue.Dequeue();
            if (dequeued is { First: Node node })
            {
                if (node is LeafNode leaf)
                {
                    var entries = leaf.GetEntries();
                    var dominatingEntries = DominatingEntries(entries);
                    foreach (var entry in dominatingEntries)
                        nodeQueue.Enqueue(entry, entry.Location.ManhattanDistanceFrom(origin));
                }
                else
                {
                    var parent = node as ParentNode;
                    
                    var children = parent!.GetChildren();
                    var dominatingNodes = DominatingNodes(children);
                    foreach (var child in dominatingNodes)
                        nodeQueue.Enqueue(child, child.Region.AsboluteExtremumDistanceFrom(origin, dominatingExtremum));
                }
            }
            else
            {
                var entry = dequeued.Second!;
                foreach (var existingEntry in result)
                {
                    var domination = existingEntry.Location.ResolveDomination(entry.Location, dominatingExtremum);
                    if (domination is Domination.Dominant)
                        goto nextDequeue;

                    // There should never be a Domination.Subordinate result, as per the algorithm
                    Debug.Assert(domination is not Domination.Subordinate);
                }

                result.Add(entry);
            
            nextDequeue:
                continue;
            }
        }

        return result;
        
        IEnumerable<TValue> DominatingEntries(IEnumerable<TValue> entries)
        {
            var comparer = new Point.OriginDistanceComparer(origin);
            var sortedEntries = entries.ToArray().SortBy(new ILocated.LocationComparer<TValue, Point.OriginDistanceComparer>(comparer)).ToList();

            RemoveDominatedValues(sortedEntries, entry => entry.Location);

            return sortedEntries;
        }
        IEnumerable<Node> DominatingNodes(IEnumerable<Node> nodes)
        {
            var comparer = new Rectangle.ExtremumPointDistanceFromOriginComparer(dominatingExtremum, origin);
            var sortedNodes = nodes.ToArray().SortBy(RegionComparerFrom(comparer)).ToList();

            RemoveDominatedValues(sortedNodes, node => node.Region);

            return sortedNodes;
        }

        static Node.RegionComparer<T> RegionComparerFrom<T>(T comparer)
            where T : IComparer<Rectangle>
        {
            return new(comparer);
        }
        void RemoveDominatedValues<TEntry, TDominable>(List<TEntry> sortedEntries, Func<TEntry, TDominable> dominableSelector)
            where TDominable : IDominable<TDominable>
        {
            // The iteration order seems fine
            for (int i = 0; i < sortedEntries.Count; i++)
            {
                var dominant = sortedEntries[i];
                for (int j = sortedEntries.Count - 1; j > i; j--)
                {
                    var subordinate = sortedEntries[j];
                    var domination = dominableSelector(dominant).ResolveDomination(dominableSelector(subordinate), dominatingExtremum);
                    if (domination is not Domination.Indeterminate)
                    {
                        Debug.Assert(domination is Domination.Dominant);

                        sortedEntries.RemoveAt(j);
                    }
                }
            }
        }
    }

    public IEnumerable<TValue> NearestNeighborQuery(int neighbors)
    {
        throw new NotImplementedException("The k-nn query did not make it in time :(");
    }

    public IEnumerable<TValue> RangeQuery<TShape>(TShape range)
        where TShape : IOverlappableWith<Rectangle>
    {
        if (range.Rank != RecordBufferController.HeaderBlock.Dimensionality)
        {
            throw new ArgumentException("The range shape must have the same rank as the points stored in the database.");
        }

        var result = new List<TValue>();

        var nodeQueue = new Queue<Node>();
        nodeQueue.Enqueue(root!);

        while (nodeQueue.Count > 0)
        {
            var iteratedNode = nodeQueue.Dequeue();
            
            switch (iteratedNode)
            {
                case LeafNode leaf:
                    var entries = leaf.GetEntries();
                    var filteredEntries = entries.Where(entry => range.Contains(entry.Location));
                    result.AddRange(filteredEntries);
                    break;

                case ParentNode parent:
                    var children = parent.GetChildren();
                    var filteredChildren = children.Where(child => range.Overlaps(child.Region));
                    nodeQueue.EnqueueRange(filteredChildren);
                    break;
            }
        }

        return result;
    }

    /// <summary>Verifies the integrity of the tree by ensuring valid children counts and the rectangles being proper MBRs.</summary>
    public bool VerifyIntegrity()
    {
        if (root is null)
            return true;

        if (!root.IsRoot)
            return false;

        return VerifyIntegrity(root);
    }
    private bool VerifyIntegrity(Node node)
    {
        // No node pointed at should be invalid
        if (node.IsInvalid)
            return false;

        switch (node)
        {
            case ParentNode parent:
                foreach (var child in parent.GetChildren())
                {
                    bool contained = parent.Region.Contains(child.Region, true);
                    if (!contained)
                        return false;

                    bool sane = VerifyIntegrity(child);
                    if (!sane)
                        return false;
                }
                return true;

            case LeafNode leaf:
                foreach (var entry in leaf.GetEntries())
                {
                    bool contained = leaf.Region.Contains(entry.Location, true);
                    if (!contained)
                        return false;
                }
                return VerifyIntegrity(leaf);
                
                // Unexpected node type
            default:
                return false;
        }
    }

    private class BulkDesignNode
    {
        public readonly SortedSet<int> EntryIDs = new();
        
        public int ID { get; set; }
        public int ParentID { get; set; }

        public Rectangle Region { get; set; }

        public BulkDesignNode(int id, int parentID, IEnumerable<TValue> entries)
        {
            ID = id;
            ParentID = parentID;

            Region = Rectangle.CreateForPoints(entries.Select(entry => entry.Location).ToArray());
            EntryIDs.AddRange(entries.Select(entry => entry.ID));
        }

        public BulkDesignNode(int id, int parentID, Rectangle region)
        {
            ID = id;
            ParentID = parentID;
            Region = region;
        }

        public LeafNode FinalizeAsLeafNode(RStarTree<TValue> tree)
        {
            return new(tree, ID, ParentID, EntryIDs, Region);
        }

        public ParentNode FinalizeAsParentNode(RStarTree<TValue> tree, IEnumerable<BulkDesignNode> childrenDesignNodes)
        {
            return new(tree, ID, ParentID, childrenDesignNodes.Select(child => child.ID), Region);
        }
    }

    private void InitializeRoot(TValue value)
    {
        root = new LeafNode(this, NodeID.Root, NodeID.Null, Enumerable.Empty<int>(), Rectangle.FromSinglePoint(value.Location));
    }

    private LazyNode? GetLazyNode(int id) => GetLazyNode(new NodeID(id));
    private LazyNode? GetLazyNode(NodeID id)
    {
        if (id.IsNull)
            return null;

        return new LazyNode(this, id);
    }

    private Node? GetNode(int id) => GetNode(new NodeID(id));
    private Node? GetNode(NodeID id)
    {
        if (id.IsNull)
            return null;
        
        var dataSpan = TreeBufferController.LoadDataSpan(id);
        return Node.ParseFromTree(this, dataSpan, id);
    }
    // TODO: Abstract this responsibility to either of the two places
    // Preferably the spatial data table
    private TValue GetEntry(int id)
    {
        var entrySpan = RecordBufferController.LoadDataSpan(id);
        return IRecordSerializable<TValue>.Parse(entrySpan, RecordBufferController.HeaderBlock, id);
    }

    // Ref-able properties would be healthy here
    private int AllocateNextID()
    {
        return serialIDHandler.AllocateNextID(TreeBufferController);
    }

    private void ScanAssignPreviousMaxID()
    {
        serialIDHandler.ScanAssignPreviousMaxID(IsValidNode);
    }

    private bool IsValidNode(int id)
    {
        return GetNode(id)?.IsInvalid is false;
    }

    public class RStarTreeBufferController : EntryBufferController
    {
        private readonly RStarTree<TValue> tree;
        
        protected override Information EntrySize => treeNodeSize.Bytes();
        public override Information BlockSize => tree.RecordBufferController.HeaderBlock.BlockSize;

        protected override int GetEntryPositionOffset(int entryID)
        {
            return base.GetEntryPositionOffset(entryID - 1);
        }

        public RStarTreeBufferController(RStarTree<TValue> tree, ChildBufferController other)
            : base(other)
        {
            this.tree = tree;
        }
    }

    public abstract class Node
    {
        protected SortedSet<int> ChildrenIDs;
        
        public RStarTree<TValue> Tree { get; }

        public virtual Rectangle Region { get; private set; }

        public abstract NodeType NodeType { get; }

        public bool WriteUponDestruction { get; set; } = true;

        public int ID { get; }
        public NodeID ParentID { get; set; }

        // TODO: Reconsider non-abstract
        public abstract int ChildrenCount { get; }
        public bool IsRoot => ParentID.IsNull;

        public bool IsFull => ChildrenIDs.Count >= Tree.MaxChildren;
        public bool IsEmpty => ChildrenIDs.Count is 0;

        public bool IsInvalid => (ChildrenIDs.Count > Tree.MaxChildren)
                              || this is
                              {
                                  NodeType: NodeType.Leaf,
                                  ChildrenIDs.Count: 0,
                              };

        public bool ShouldMerge => ChildrenIDs.Count < Tree.MinChildren;
        public bool ShouldSplit => ChildrenIDs.Count > Tree.MinChildren;

        protected Node(RStarTree<TValue> tree, int id, int parentID, IEnumerable<int> childrenIDs, Rectangle region)
            : this(tree, id)
        {
            ParentID = parentID;
            ChildrenIDs = new(childrenIDs);
            Region = region;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected Node(RStarTree<TValue> tree, int id)
        {
            Tree = tree;
            ID = id;
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        ~Node()
        {
            if (WriteUponDestruction)
                WriteChangesToBuffer();
        }

        public Node GetFullyLoadedNode()
        {
            if (this is not LeafNode)
                return this;
            
            return Tree.GetNode(ID)!;
        }

        public Span<byte> GetDataSpan(out DataBlock dataBlock)
        {
            return Tree.TreeBufferController.LoadDataSpan(ID, out dataBlock);
        }

        public ParentNode? GetParent()
        {
            if (ParentID.IsNull)
                return null;

            return Tree.GetNode(ParentID) as ParentNode;
        }
        
        public void SwapParent()
        {
            var parentNode = Tree.GetNode(ParentID);
            if (parentNode is null)
                return;
            
            UpdateParent(parentNode.ParentID);
            parentNode.UpdateParent(ID);
        }
        
        public void UpdateParent(ParentNode? node)
        {
            UpdateParent(node?.ID ?? NodeID.Null);
        }
        public void UpdateParent(int newParentID)
        {
            int oldParentID = ParentID;
            var oldParentNode = Tree.GetNode(oldParentID);
            var newParentNode = Tree.GetNode(newParentID);
            ParentID = newParentID;

            oldParentNode?.ChildrenIDs.Remove(ID);
            newParentNode?.ChildrenIDs.Add(ID);
        }

        public void EnsureRegionContains(Point point)
        {
            Region = Region.Expand(point);
        }

        public abstract IEnumerable<Rectangle> GetChildrenRectangles();

        public abstract Node TrimRegion(Rectangle newRegion);

        // TODO: Override in LazyNode to avoid writing the children and region, if not loaded
        public virtual void WriteChangesToBuffer()
        {
            Debug.Assert(ChildrenIDs.Count <= Tree.MaxChildren, "Attempted to serialize a node with more children than the max allowed.");

            var dataSpan = GetDataSpan(out var dataBlock);
            var writer = new SpanStream(dataSpan);

            WriteParentID(ref writer);
            WriteRegion(ref writer);
            WriteTypeAndChildrenCount(ref writer);
            WriteChildrenIDs(ref writer);

            // Usually some node in that block will have caused a change
            Tree.TreeBufferController.MarkDirty(dataBlock);
        }

        protected void WriteParentID(ref SpanStream writer)
        {
            writer.WriteValue(ParentID);
        }
        protected void SkipParentID(ref SpanStream writer)
        {
            writer.AdvanceValue<int>();
        }

        protected void WriteRegion(ref SpanStream writer)
        {
            for (int i = 0; i < Region.Rank; i++)
                writer.WriteValue((float)Region.MinPoint.GetCoordinate(i));

            for (int i = 0; i < Region.Rank; i++)
                writer.WriteValue((float)Region.MaxPoint.GetCoordinate(i));
        }
        protected void SkipRegion(ref SpanStream writer)
        {
            writer.AdvanceValueRange<float>(2 * Region.Rank);
        }
        protected void WriteOrSkipRegion(ref SpanStream writer)
        {
            if (Region is null)
                SkipRegion(ref writer);
            else
                WriteRegion(ref writer);
        }

        protected void WriteTypeAndChildrenCount(ref SpanStream writer)
        {
            var typeAndChildren = new TypeAndChildren(NodeType, ChildrenIDs.Count);
            writer.WriteValue(typeAndChildren.SerializedByte);
        }
        protected void SkipTypeAndChildrenCount(ref SpanStream writer)
        {
            writer.AdvanceValue<byte>();
        }

        protected void WriteChildrenIDs(ref SpanStream writer)
        {
            var typeAndChildren = new TypeAndChildren(NodeType, ChildrenIDs.Count);
            writer.WriteValue(typeAndChildren.SerializedByte);
        }
        protected void SkipChildrenIDs(ref SpanStream writer)
        {
            writer.AdvanceValueRange<int>(ChildrenIDs.Count);
        }
        protected void WriteOrSkipChildrenIDs(ref SpanStream writer)
        {
            if (ChildrenIDs is null)
                SkipChildrenIDs(ref writer);
            else
                WriteChildrenIDs(ref writer);
        }

        public static Node ParseFromTree(RStarTree<TValue> tree, Span<byte> nodeBytes, int id)
        {
            var reader = new SpanStream(nodeBytes);
            int parentID = reader.ReadValue<int>();

            int dimensions = tree.RecordBufferController.HeaderBlock.Dimensionality;

            // Cannot simplify to local functions due to Span<byte> being ref struct
            double[] min = new double[dimensions];
            for (int i = 0; i < dimensions; i++)
                min[i] = reader.ReadValue<float>();
            
            double[] max = new double[dimensions];
            for (int i = 0; i < dimensions; i++)
                max[i] = reader.ReadValue<float>();
            
            var region = Rectangle.FromVertices(new(min), new(max));

            byte typeAndChildrenByte = reader.ReadValue<byte>();
            var (nodeType, childrenCount) = TypeAndChildren.FromByte(typeAndChildrenByte);
            
            var childrenArray = ChildrenIDArrayPool.Array;
            var childrenSpan = new ArraySegment<int>(childrenArray, 0, childrenCount);
            for (int i = 0; i < childrenCount; i++)
                childrenArray[i] = reader.ReadValue<int>();

            return nodeType switch
            {
                NodeType.Parent => new ParentNode(tree, id, parentID, childrenSpan, region),
                NodeType.Leaf => new LeafNode(tree, id, parentID, childrenSpan, region),

                // Unexpected code path, should never be reached
                _ => null!,
            };
        }

        private static class ChildrenIDArrayPool
        {
            // 62 - 2n,
            //   where n > 0
            private const int MaxChildren = 60;
            public static readonly int[] Array = new int[MaxChildren];
        }

        public record struct RegionComparer<TComparer>(TComparer Comparer) : IComparer<Node>
            where TComparer : IComparer<Rectangle>
        {
#nullable disable
            public int Compare(Node x, Node y)
            {
                return Comparer.Compare(x.Region, y.Region);
            }
#nullable restore
        }
    }

    private record struct TypeAndChildren(NodeType NodeType, int ChildrenCount)
    {
        private const int NodeTypeFlagMask = 0b_1000_0000;

        public byte SerializedByte => (byte)((int)NodeType * NodeTypeFlagMask | ChildrenCount);

        public static TypeAndChildren FromByte(byte b)
        {
            var nodeType = (NodeType)((b & NodeTypeFlagMask) / NodeTypeFlagMask);
            int childrenCount = b & ~NodeTypeFlagMask;
            return new(nodeType, childrenCount);
        }
    }

    public sealed class LazyNode : Node
    {
        private NodeType nodeType = NodeType.Unknown;
        private int childrenCount = -1;
        private Rectangle? region = null;

        public IEnumerable<int> ChildrenNodeIDs
        {
            get
            {
                if (ChildrenIDs is null)
                    LoadChildren();

                return ChildrenIDs!;
            }
        }
        
        public override NodeType NodeType
        {
            get
            {
                if (nodeType is NodeType.Unknown)
                    LoadNodeTypeAndChildrenCount();

                return nodeType;
            }
        }
        public override int ChildrenCount
        {
            get
            {
                if (childrenCount < 0)
                    LoadNodeTypeAndChildrenCount();

                return childrenCount;
            }
        }

        public override Rectangle Region
        {
            get
            {
                if (region is null)
                    LoadRegion();

                return region!;
            }
        }

        public LazyNode(RStarTree<TValue> tree, int id)
            : base(tree, id) { }

        public override IEnumerable<Rectangle> GetChildrenRectangles()
        {
            return GetFullyLoadedNode().GetChildrenRectangles();
        }

        private Span<byte> GetDataSpan()
        {
            return Tree.TreeBufferController.LoadDataSpan(ID);
        }

        public override Node TrimRegion(Rectangle newRegion)
        {
            throw new InvalidOperationException("The lazy node should not be able to be trimmed.");
        }

        public override void WriteChangesToBuffer()
        {
            var dataSpan = GetDataSpan(out var dataBlock);
            var writer = new SpanStream(dataSpan);

            WriteParentID(ref writer);
            
            if (region is not null)
                WriteRegion(ref writer);
            else
            WriteTypeAndChildrenCount(ref writer);
            WriteChildrenIDs(ref writer);

            // Usually some node in that block will have caused a change
            Tree.TreeBufferController.MarkDirty(dataBlock);
        }

        // The loading has been copy-pasted
        public void LoadRegion()
        {
            var dataSpan = GetDataSpan();
            int offset = GetOffsetForRegionCoordinates();
            dataSpan = dataSpan[offset..];
            var reader = new SpanStream(dataSpan);

            int dimensions = Tree.Dimensionality;
            double[] min = new double[dimensions];
            for (int i = 0; i < dimensions; i++)
                min[i] = reader.ReadValue<float>();

            double[] max = new double[dimensions];
            for (int i = 0; i < dimensions; i++)
                max[i] = reader.ReadValue<float>();

            region = Rectangle.FromVertices(new(min), new(max));
        }
        public void LoadChildren()
        {
            var dataSpan = GetDataSpan();
            int offset = GetOffsetForChildrenIDs();
            dataSpan = dataSpan[offset..];
            var reader = new SpanStream(dataSpan);
            int count = ChildrenCount;

            var tempIDs = new int[count];
            for (int i = 0; i < count; i++)
                tempIDs[i] = reader.ReadValue<int>();

            ChildrenIDs = new(tempIDs);
        }
        public void LoadParentID()
        {
            var dataSpan = GetDataSpan();
            var reader = new SpanStream(dataSpan);
            ParentID = reader.ReadValue<int>();
        }
        public void LoadNodeTypeAndChildrenCount()
        {
            var span = GetDataSpan();
            int offset = GetOffsetForNodeTypeChildrenCount();
            span = span[offset..];
            byte typeAndChildren = span.ReadValue<byte>();
            (nodeType, childrenCount) = TypeAndChildren.FromByte(typeAndChildren);
        }

        private int GetOffsetForParentID() => 0;
        private int GetOffsetForRegionCoordinates() => GetOffsetForParentID() + sizeof(int);
        private int GetOffsetForNodeTypeChildrenCount() => GetOffsetForRegionCoordinates() + 2 * Tree.Dimensionality * sizeof(float);
        private int GetOffsetForChildrenIDs() => GetOffsetForNodeTypeChildrenCount() + sizeof(byte);
    }

    public sealed class ParentNode : Node
    {
        public override NodeType NodeType => NodeType.Parent;
        
        public override int ChildrenCount => ChildrenIDs.Count;

        public ParentNode(RStarTree<TValue> tree, int id, int parentID, IEnumerable<int> childrenIDs, Rectangle region)
            : base(tree, id, parentID, childrenIDs, region)
        {
        }

        public IEnumerable<LazyNode> GetLazyChildren() => ChildrenIDs.Select(Tree.GetLazyNode)!;

        public IEnumerable<Node> GetChildren() => ChildrenIDs.Select(Tree.GetNode) as IEnumerable<Node>;

        public override ParentNode TrimRegion(Rectangle newRegion)
        {
            var children = GetChildren();
            children.Dissect(child => newRegion.Contains(child.Region), out var inside, out var outside);
            // Locally cache the values
            inside = inside.ToList(ChildrenCount);
            outside = outside.ToList(ChildrenCount);

            ChildrenIDs = new(inside.Select(c => c.ID));
            return new ParentNode(Tree, NodeID.Null, ParentID, outside.Select(c => c.ID), newRegion);
        }

        public bool IsParentOf(Node child) => child.ParentID == ID;

        public double GetTotalChildrenOverlappingArea(Node targetChild)
        {
            return GetTotalChildrenOverlappingArea(targetChild, targetChild.Region);
        }
        public double GetTotalChildrenOverlappingArea(Node targetChild, Rectangle customRegion)
        {
            // Assume correct
            if (!IsParentOf(targetChild))
                return -1;

            var children = GetLazyChildren().ToArray();
            double totalOverlap = 0;

            foreach (var otherChild in children)
            {
                if (otherChild.ID == targetChild.ID)
                    continue;

                double overlap = customRegion.OverlappingArea(otherChild.Region);
                totalOverlap += overlap;
            }
            return totalOverlap;
        }

        public Node? ChildContaining(Point point)
        {
            return GetLazyChildren().FirstOrDefault(child => child.Region.Contains(point, true));
        }

        public void NotifyChildrenParenthood()
        {
            foreach (var childID in ChildrenIDs)
            {
                var childNode = Tree.GetLazyNode(childID)!;
                // We're just informing our children that they are adopted
                // They don't quite have the freedom of updating their parents themselves
                // What a dark time to live in
                childNode.ParentID = ID;
                childNode.WriteChangesToBuffer();
            }
        }

        public void AddChild(Node childNode)
        {
            ChildrenIDs.Add(childNode.ID);
            childNode.UpdateParent(this);
        }

        public override IEnumerable<Rectangle> GetChildrenRectangles()
        {
            return GetLazyChildren().Select(n => n.Region);
        }
    }
    public sealed class LeafNode : Node
    {
        public override int ChildrenCount => 0;
        public int EntryCount => ChildrenIDs.Count;

        public override NodeType NodeType => NodeType.Leaf;
        
        public LeafNode(RStarTree<TValue> tree, int id, int parentID, IEnumerable<int> childrenIDs, Rectangle region)
            : base(tree, id, parentID, childrenIDs, region)
        {
        }

        public override LeafNode TrimRegion(Rectangle newRegion)
        {
            // TODO:
            return throw new NotImplementedException();
        }

        public IEnumerable<TValue> GetEntries() => ChildrenIDs.Select(Tree.GetEntry);

        public TValue? ValueAt(Point point) => GetEntries().FirstOrDefault(entry => entry.Location == point);

        public override IEnumerable<Rectangle> GetChildrenRectangles()
        {
            return GetEntries().Select(e => Rectangle.FromSinglePoint(e.Location));
        }
    }

    public delegate void NodeSectionTraverser(ref SpanStream writer);

    public enum NodeSectionTraversalMode
    {
        Skip,
        Write,
    }

    public record struct NodeSectionTraversers(NodeSectionTraverser Writer, NodeSectionTraverser Skipper)
    {
        public NodeSectionTraverser TraverserForMode(NodeSectionTraversalMode mode) => mode switch
        {
            NodeSectionTraversalMode.Write => Writer,
            _ => Skipper,
        };

        public void Perform(NodeSectionTraversalMode mode, ref SpanStream writer)
        {
            TraverserForMode(mode)(ref writer);
        }
    }

    public record struct NodeID(int ID)
    {
        // It is considered that the root ID is 1
        private const int rootID = 1;
        private const int nullID = 0;

        public static readonly NodeID Null = new(nullID);
        public static readonly NodeID Root = new(rootID);

        public bool IsNull => ID <= nullID;
        public bool IsRoot => ID is rootID;

        public static implicit operator NodeID(int id) => new(id);
        public static implicit operator int(NodeID id) => id.ID;
    }
    
    public enum NodeType
    {
        Unknown = -1,

        Parent = 0,
        Leaf,
    }
}
