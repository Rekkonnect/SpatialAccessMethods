using Garyon.DataStructures;
using Garyon.Extensions;
using Garyon.Extensions.ArrayExtensions;
using Garyon.Objects;
using Microsoft.CodeAnalysis;
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

    // Current operation reference disposal should ONLY occur in public methods
    // By design only publicly consumed methods are considered full atomic operations
    private readonly OperationReferenceFactory operationReferenceFactory = new();
    private readonly SerialIDHandler serialIDHandler;

    public int Order { get; }

    public bool IsEmpty => Root is null;

    ChildBufferController ISecondaryStorageDataStructure.BufferController => TreeBufferController;

    public RStarTreeBufferController TreeBufferController { get; }
    public RecordEntryBufferController RecordBufferController { get; }

    public int Dimensionality => RecordBufferController.HeaderBlock.Dimensionality;

    private int nodeCount;
    private RStarTree<TValue>.Node? root;

    public int NodeCount
    {
        get => nodeCount;
        private set
        {
            if (nodeCount == value)
                return;

            SetNodeCountWithoutResize(value);
            TreeBufferController.ResizeForEntryCount(value);
        }
    }

    // Always strive for >= 50% capacity
    public int MinChildren => (Order + 1) / 2;
    public int MaxChildren => Order;

    public int Height => GetHeightForEntryCount(GetEntryCount());

    public Node? Root
    {
        get => root;
        private set
        {
            Debug.Assert(value?.ID is null or NodeID.RootID);

            DethroneRoot();
            root = value;
        }
    }

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
            ValidIDPredicate = IsValidNode,
        };
    }

    private int GetMaxID() => RecordBufferController.HeaderBlock.MaxTreeNodeID;
    private void SetMaxID(int maxID) => RecordBufferController.HeaderBlock.MaxTreeNodeID = maxID;
    private int GetEntryCount() => RecordBufferController.HeaderBlock.TreeNodeCount;

    private void SetNodeCountWithoutResize(int value)
    {
        nodeCount = value;
        RecordBufferController.HeaderBlock.TreeNodeCount = value;
    }
    private void IncreaseNodeCountWithoutResize()
    {
        nodeCount++;
        RecordBufferController.HeaderBlock.TreeNodeCount = nodeCount;
    }
    private void DecreaseNodeCountWithoutResize()
    {
        nodeCount--;
        RecordBufferController.HeaderBlock.TreeNodeCount = nodeCount;
    }
    private void IncreaseNodeCount()
    {
        IncreaseNodeCountWithoutResize();
        EnsureLengthForNewNode();
    }
    private void DecreaseNodeCount()
    {
        DecreaseNodeCountWithoutResize();
        EnsureLengthForNewNode();
    }
    private void EnsureLengthForNewNode()
    {
        TreeBufferController.EnsureLengthForEntry(nodeCount + 1);
    }

    public LeafNode? LeafContainingValue(Point point, out TValue? value)
    {
        // Get all the leafs that overlap with the requested location
        var leafs = LeafsContaining(point);

        TValue? internalValue = default;

        // Identify the leaf that actually contains the given entry
        var leaf = leafs.FirstOrDefault(leaf =>
        {
            var value = leaf.ValueAt(point);
            if (value?.IsValid != true)
                return false;

            internalValue = value;
            return true;
        });

        value = internalValue;
        return leaf;
    }
    public LeafNode? LeafContainingValue(TValue value)
    {
        return LeafContainingValue(value.Location, out _);
    }
    public IEnumerable<LeafNode> LeafsContaining(Point point)
    {
        if (Root is null)
            yield break;

        var queue = new Queue<Node>();
        queue.Enqueue(Root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is LeafNode leaf)
                yield return leaf;

            var currentParent = current as ParentNode;
            var children = currentParent!.ChildrenContaining(point);
            queue.EnqueueRange(children);
        }
    }
    public TValue? GetValue(Point point)
    {
        var leafs = LeafsContaining(point);
        if (leafs is null)
            return default;

        foreach (var leaf in leafs)
        {
            var value = leaf.ValueAt(point);
            if (value?.IsValid == true)
                return value;
        }

        return default;
    }

    public void Clear()
    {
        Root = null;
        NodeCount = 0;
        RecordBufferController.HeaderBlock.MaxTreeNodeID = 0;
        serialIDHandler.Clear();
    }

    private LeafNode? ChooseLeaf(Point point)
    {
        return LeafsContaining(point).FirstOrDefault();
    }
    private LeafNode ChooseLeafInsert(Point point)
    {
        return ChooseLeaf(point)
            ?? ChooseSubtree(point);
    }

    private LeafNode ChooseSubtree(TValue entry)
    {
        return ChooseSubtree(entry.Location);
    }
    private LeafNode ChooseSubtree(Point location)
    {
        var node = Root;

        while (true)
        {
            if (node is LeafNode leaf)
                return leaf;

            var parentNode = node as ParentNode;
            var children = parentNode!.GetChildren().ToArray();

            var comparer = BestSubtreeInfo.PreferenceScoreComparer.CreateForNodeType(children.First().NodeType);

            var bestSubtree = new BestSubtreeInfo(location, comparer);
            bestSubtree.RegisterNodes(children);
            node = bestSubtree.BestNode;
        }
    }
    private Node ChooseSubtree(int level, Node node)
    {
        var currentNode = Root;

        int currentLevel = 0;
        while (currentLevel <= level)
        {
            if (currentNode is LeafNode)
                return currentNode;

            var parentNode = currentNode as ParentNode;
            var children = parentNode!.GetChildren().ToArray();

            var comparer = BestSubtreeInfo.PreferenceScoreComparer.CreateForNodeType(children.First().NodeType);

            var bestSubtree = new BestSubtreeInfo(node, comparer);
            bestSubtree.RegisterNodes(children);
            currentNode = bestSubtree.BestNode;
        }
        return currentNode;
    }

    private void ReInsert(int level, Node node)
    {
        const double removedChildrenRate = 0.30; // as the paper suggests

        // We assume that node contains M+1 entries;
        // which is allowed outside the context of serialization

        switch (node)
        {
            // Copy-paste go!
            case LeafNode leaf:
            {
                var entries = leaf.GetEntries().ToArray();
                // Ascending
                var distances = entries
                    .Select(e => EntryDistance.FromDistance(e, node.Region))
                    .ToArray()
                    .SortBy((a, b) => a.Distance.CompareTo(b.Distance));

                int removedCount = (int)(removedChildrenRate * distances.Length);
                int newEntryTotal = distances.Length - removedCount;
                var removedDistances = distances[newEntryTotal..];

                // Remove the children IDs that are filtered out
                node.ChildrenIDs.ExceptWith(removedDistances.Select(d => d.Entry.ID));

                foreach (var distance in removedDistances)
                {
                    Insert(distance.Entry);
                }
                break;
            }

            case ParentNode parent:
            {
                var children = parent.GetChildren().ToArray();
                // Ascending
                var distances = children
                    .Select(child => NodeDistance.FromDistance(child, node.Region))
                    .ToArray()
                    .SortBy((a, b) => a.Distance.CompareTo(b.Distance));

                int removedCount = (int)(removedChildrenRate * distances.Length);
                int newEntryTotal = distances.Length - removedCount;
                var removedDistances = distances[newEntryTotal..];

                // Remove the children IDs that are filtered out
                node.ChildrenIDs.ExceptWith(removedDistances.Select(d => d.Node.ID));

                foreach (var distance in removedDistances)
                {
                    // level + 1?
                    Insert(level, distance.Node);
                }

                break;
            }
        }
    }

    private record struct NodeDistance(Node Node, double Distance)
    {
        public static NodeDistance FromDistance(Node node, Rectangle rectangle)
        {
            var distance = rectangle.Center.DistanceFrom(node.Region.Center);
            return new(node, distance);
        }
    }

    private record struct EntryDistance(TValue Entry, double Distance)
    {
        public static EntryDistance FromDistance(TValue value, Rectangle rectangle)
        {
            var distance = rectangle.Center.DistanceFrom(value.Location);
            return new(value, distance);
        }
    }

    private OverflowTreatmentResult OverflowTreatment(object operationReference, int level, Node node)
    {
        var stats = OverflowTreatmentStats.RecordInvocation(operationReference, level);
        if (level > 0 && stats.CounterAtLevel(level) <= 1)
        {
            ReInsert(level, node);
            return OverflowTreatmentResult.ReInsert;
        }
        else
        {
            Split(level, node);
            return OverflowTreatmentResult.Split;
        }
    }

    private enum OverflowTreatmentResult
    {
        ReInsert,
        Split,
    }

    private class OverflowTreatmentStats : IDisposable
    {
        private static readonly Dictionary<object, OverflowTreatmentStats> statsMap = new();

        private readonly object reference;

        private readonly ValueCounterDictionary<int> levelCounters = new();

        private OverflowTreatmentStats(object reference)
        {
            this.reference = reference;
        }
        ~OverflowTreatmentStats()
        {
            Dispose();
        }

        public void RecordInvocation(int level)
        {
            levelCounters[level]++;
        }
        public int CounterAtLevel(int level)
        {
            return levelCounters[level];
        }

        public void Dispose()
        {
            statsMap.Remove(reference);
            GC.SuppressFinalize(this);
        }

        public static OverflowTreatmentStats RecordInvocation(object operationReference, int level)
        {
            var stats = ForInstance(operationReference);
            stats.RecordInvocation(level);
            return stats;
        }

        public static OverflowTreatmentStats ForInstance(object reference)
        {
            statsMap.TryGetValue(reference, out var stats);
            if (stats is null)
            {
                stats = new(reference);
                statsMap[reference] = stats;
            }
            return stats;
        }
    }

    private void DethroneRoot()
    {
        if (root is null)
            return;

        root.ID = AllocateNextID();
        root.WriteChangesToBuffer();
    }

    private void Split(int level, Node node)
    {
        const double orderRate = 0.40;
        var splitDistribution = AttemptSplit(orderRate);

        var leftRegion = splitDistribution.LeftMBR;
        var rightNode = node.TrimRegion(leftRegion);
        if (level is 0)
        {
            // We split the root, so we need to create a new one
            var rootRegion = Rectangle.CreateForRectangles(node.Region, rightNode.Region);
            var newRoot = AllocateNewParentRootNode(Array.Empty<int>(), rootRegion);
            Root = newRoot;
            // The dethroned node will have had its ID changed to a newly allocated one
            newRoot.AddChild(node);
            newRoot.AddChild(rightNode);
            IncreaseNodeCount();
        }
        else
        {
            Insert(level, rightNode);
        }

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
            return rectangleDistributions.Min(RectangleDistribution.Comparer.Instance)!;
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

        public double VolumeValue => RStarTree<TValue>.VolumeValue(LeftMBR, RightMBR);
        public double MarginValue => RStarTree<TValue>.MarginValue(LeftMBR, RightMBR);
        public double OverlapValue => RStarTree<TValue>.OverlapValue(LeftMBR, RightMBR);

        public sealed class Comparer : IComparer<RectangleDistribution>
        {
            public static Comparer Instance { get; } = new();
            private Comparer() { }

#nullable disable
            public int Compare(RectangleDistribution a, RectangleDistribution b)
            {
                int comparison = a.OverlapValue.CompareTo(b.OverlapValue);
                if (comparison is not 0)
                    return comparison;

                return a.VolumeValue.CompareTo(b.VolumeValue);
            }
#nullable restore
        }
    }

    private class EntryOrNode
    {
        public Optional<TValue> Entry { get; }
        public Optional<Node> Node { get; }

        public bool IsEntry => Entry.HasValue;
        public bool IsNode => Node.HasValue;

        public EntryOrNode(TValue entry)
            : this(entry, default) { }

        public EntryOrNode(Node node)
            : this(default, node) { }

        private EntryOrNode(Optional<TValue> entry, Optional<Node> node)
        {
            Entry = entry;
            Node = node;
        }

        public static implicit operator EntryOrNode(TValue value) => new(value);
        public static implicit operator EntryOrNode(Node node) => new(node);
    }

#nullable disable
    private class BestSubtreeInfo
    {
        private readonly PreferenceScoreComparer preferenceScoreComparer;

        public Point Location { get; }
        public EntryOrNode EntryOrNode { get; }

        public PreferenceScore Best { get; private set; }

        public Node BestNode => Best.Node;

        public BestSubtreeInfo(EntryOrNode entryOrNode, PreferenceScoreComparer comparer)
        {
            EntryOrNode = entryOrNode;
            preferenceScoreComparer = comparer;
        }
        public BestSubtreeInfo(Point location, PreferenceScoreComparer comparer)
        {
            Location = location;
            preferenceScoreComparer = comparer;
        }

        public void RegisterNodes(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
                RegisterNode(node);
        }

        public void RegisterNode(Node node)
        {
            var preferenceScore = InitializePreferenceScore(node);

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

        private PreferenceScore InitializePreferenceScore(Node node)
        {
            if (EntryOrNode is not null)
                return new(node, EntryOrNode);

            return PreferenceScore.GetForEntryLocation(node, Location);
        }

        public sealed class LeafChildrenComparer : PreferenceScoreComparer
        {
            public static LeafChildrenComparer Instance { get; } = new();
            private LeafChildrenComparer() { }

            public override int Compare(PreferenceScore left, PreferenceScore right)
            {
                int comparison = left.OverlapEnlargement.CompareTo(right.OverlapEnlargement);
                if (comparison is not 0)
                    return comparison;

                return CompareAreaAndBelow(left, right);
            }
        }
        public sealed class ParentChildrenComparer : PreferenceScoreComparer
        {
            public static ParentChildrenComparer Instance { get; } = new();
            private ParentChildrenComparer() { }

            public override int Compare(PreferenceScore left, PreferenceScore right)
            {
                return CompareAreaAndBelow(left, right);
            }
        }

        public abstract class PreferenceScoreComparer : IComparer<PreferenceScore>
        {
            public abstract int Compare(PreferenceScore left, PreferenceScore right);

            protected static int CompareAreaAndBelow(PreferenceScore left, PreferenceScore right)
            {
                int comparison = left.AreaEnlargement.CompareTo(right.AreaEnlargement);
                if (comparison is not 0)
                    return comparison;

                return left.Area.CompareTo(right.Area);
            }

            public static PreferenceScoreComparer CreateForNodeType(NodeType type) => type switch
            {
                NodeType.Leaf => LeafChildrenComparer.Instance,
                NodeType.Parent => ParentChildrenComparer.Instance,

                _ => null!,
            };
        }

        public struct PreferenceScore
        {
            public Node Node { get; }

            public double OverlapEnlargement { get; private set; }
            public double AreaEnlargement { get; private set; }
            public double Area { get; private set; }

            private PreferenceScore(Node node)
            {
                Node = node;
            }
            public PreferenceScore(Node node, EntryOrNode entryOrNode)
            {
                Node = node;
                Populate(entryOrNode);
            }

            private void Populate(EntryOrNode entryOrNode)
            {
                if (entryOrNode.IsEntry)
                {
                    Populate(entryOrNode.Entry.Value);
                }
                else
                {
                    Populate(entryOrNode.Node.Value);
                }
            }

            private void Populate(Node nextNode)
            {
                var newRegion = Node.Region.Expand(nextNode.Region);
                Populate(newRegion);
            }
            private void Populate(TValue entry)
            {
                Populate(entry.Location);
            }
            private void Populate(Point entryLocation)
            {
                var newRegion = Node.Region.Expand(entryLocation);
                Populate(newRegion);
            }

            private void Populate(Rectangle newRegion)
            {
                var parent = Node.GetParent();

                double previousOverlap = parent.GetTotalChildrenOverlappingArea(Node);
                double newOverlap = parent.GetTotalChildrenOverlappingArea(Node, newRegion);
                OverlapEnlargement = newOverlap - previousOverlap;

                double oldArea = Node.Region.Volume;
                double newArea = newRegion.Volume;
                AreaEnlargement = newArea - oldArea;
                Area = newArea;
            }

            public static PreferenceScore GetForEntryLocation(Node node, Point entryLocation)
            {
                var score = new PreferenceScore(node);
                score.Populate(entryLocation);
                return score;
            }
        }
    }
#nullable restore

    private static double OverlapValue(Rectangle a, Rectangle b)
    {
        return a.OverlappingVolume(b);
    }
    private static double VolumeValue(Rectangle a, Rectangle b)
    {
        return a.Volume + b.Volume;
    }
    private static double MarginValue(Rectangle a, Rectangle b)
    {
        return a.Margin + b.Margin;
    }

    private void Insert(int level, Node node)
    {
        int parentLevel = level - 1;
        var subtree = ChooseSubtree(parentLevel, node);
        if (subtree is not ParentNode parentSubtree)
        {
            throw new InvalidDataException("The returned subtree was expected to be a parent node.");
        }

        if (parentSubtree.IsFull)
        {
            var result = OverflowTreatment(operationReferenceFactory.CurrentOrNext(), parentLevel, parentSubtree);
            if (result is OverflowTreatmentResult.Split)
            {
                // Adjust the parent node's MBR?
            }
        }

        parentSubtree.AddChild(node);
        IncreaseNodeCount();
    }

    public void Insert(TValue value)
    {
        if (Root is null)
            InitializeRoot(value);

        var containingLeaf = ChooseLeafInsert(value.Location);

        if (containingLeaf.IsFull)
        {
            OverflowTreatment(operationReferenceFactory.CurrentOrNext(), Height, containingLeaf);
        }

        containingLeaf.AddEntry(value);

        operationReferenceFactory.DisposeCurrent();
    }

    public bool Remove(Point point)
    {
        var leaf = LeafContainingValue(point, out var value);

        if (leaf is null)
            return false;

        bool removed = leaf.ChildrenIDs.Remove(value!.ID);
        Debug.Assert(removed);

        CondenseTree(leaf);

        operationReferenceFactory.DisposeCurrent();
        return true;
    }
    public bool Remove(TValue value)
    {
        return Remove(value.Location);
    }

    private void CondenseTree(LeafNode leaf)
    {
        if (!leaf.ShouldMerge)
            return;

        // Merging is the same operation as in R-tree
        Node currentNode = leaf;
        var eliminatedNodes = new List<Node>();

        while (true)
        {
            if (currentNode.IsRoot)
                break;

            // Non-root nodes definitely have a parent                                         famous last words
            var parent = currentNode.GetParent()!;

            if (currentNode.ShouldMerge)
            {
                parent.ChildrenIDs.Remove(currentNode.ID);
                eliminatedNodes.Add(currentNode);
            }
            else
            {
                currentNode.RefreshMBR();
            }

            currentNode = parent;
        }

        foreach (var eliminatedNode in eliminatedNodes)
        {
            if (eliminatedNode is LeafNode eliminatedLeaf)
            {
                // Reinsert all leaf node entries
                var entries = eliminatedLeaf.GetEntries();
                foreach (var entry in entries)
                    Insert(entry);
            }
            else
            {
                // Reinsert the parent nodes to their respective levels
                var eliminatedParent = eliminatedNode as ParentNode;
                var childrenNodes = eliminatedParent!.GetChildren();
                foreach (var childNode in childrenNodes)
                {
                    // Reinsert the child node at the same level
                    // Find the best subtree to inlcude the new node in
                    // If this function does not suit; it should be another
                    Insert(childNode.GetLevel(), childNode);
                }
            }

            serialIDHandler.DeallocateID(TreeBufferController, eliminatedNode.ID);
            DecreaseNodeCount();
        }
    }

    public int GetHeightForEntryCount(int entryCount, double capacity = 1)
    {
        double maxEntryCount = Math.Floor(entryCount * capacity);
        return (int)Math.Ceiling(Math.Log(maxEntryCount, Order));
    }

    public void BulkLoad(IEnumerable<TValue> entries)
    {
        // Average capacity arbitrarily picked as 70%
        // to avoid overflows soon after constructing the tree
        const double averageCapacity = 0.70;

        if (!IsEmpty)
            throw new InvalidOperationException("Cannot bulk load an R*-tree that already contains nodes.");

        var entryArray = entries.ToExistingOrNewArray();
        // Reset the ID as the value is not passed through in the case of structs
        for (int i = 0; i < entryArray.Length; i++)
            entryArray[i].ID = i;

        int cappedOrder = (int)Math.Ceiling(Order * averageCapacity);
        // Using List instead of Stack for the freedom of exploring the intermediate nodes
        var levelNodeCounts = new List<int>();
        int currentLevelNodes = entryArray.Length;
        while (currentLevelNodes > Order)
        {
            int nextLevelNodes = currentLevelNodes / cappedOrder;
            levelNodeCounts.Add(nextLevelNodes);
            currentLevelNodes = nextLevelNodes;
        }
        // Create a new root if the top level has more than one nodes
        if (currentLevelNodes > 1)
        {
            levelNodeCounts.Add(1);
        }

        // **If we exclude the reallocation of the list in capacity > current**
        // n * Insert(0, value)        is O(n^2)
        // n * Add(value) + Reverse()  is O(n)
        levelNodeCounts.Reverse();

        int totalNodeCount = levelNodeCounts.Sum();
        NodeCount = totalNodeCount;

        int dimensionality = RecordBufferController.HeaderBlock.Dimensionality;
        int treeHeight = levelNodeCounts.Count;

        var rootRectangle = Rectangle.CreateForPoints(entryArray.Select(entry => entry.Location).ToArray());
        var axisRanges = AxisRange.Ranges(rootRectangle).SortBy(AxisRange.RangeComparer.Instance.Invert()).Reverse();

        StableSortEntryArray(entryArray, axisRanges, 0);

        // Allocate it beforehand to make sure that it is not being used elsewhere
        int rootID = AllocateNextID();
        Debug.Assert(rootID is NodeID.RootID);

        if (treeHeight > 1)
        {
            int rootChildrenCount = levelNodeCounts[1];
            var childrenDesignNodes = CreateDesignNodes(entryArray, rootChildrenCount, NodeID.Root);

            var rootChildrenIDs = childrenDesignNodes.Select(node => node.ID);
            Root = AllocateNewParentRootNode(rootChildrenIDs, rootRectangle);

            BulkLoadSlab(childrenDesignNodes, axisRanges, BulkLoadArguments.Initial(dimensionality, treeHeight, levelNodeCounts));
        }
        else
        {
            var entryIDs = entryArray.Select(e => e.ID);
            Root = AllocateNewLeafRootNode(entryIDs, rootRectangle);
        }
    }

    private static TValue[] StableSortEntryArray(TValue[] entryArray, AxisRange[] axisRanges, int coordinate)
    {
        var comparer = Point.CoordinateComparer.ForDimension((int)axisRanges[coordinate].Axis);
        return entryArray.SortBy(new ILocated.LocationComparer<TValue, Point.CoordinateComparer>(comparer));
    }

    [Conditional("DEBUG")]
    private void AssertNodeProperlyWritten(Node node)
    {
        var readNode = GetNode(node.ID)!;

        Debug.Assert(readNode.ID == node.ID, "Read a wrong node ID for specified node");
        Debug.Assert(readNode.NodeType == node.NodeType, "Read a wrong node type for specified node");
        Debug.Assert(readNode.Region == node.Region, "Read a wrong region for specified node");
        Debug.Assert(readNode.ChildrenIDs.SetEquals(node.ChildrenIDs), "Read a wrong children IDs for specified node");
    }

    // I don't think I have the necessary patience for this
    private void BulkLoadSlab(IEnumerable<BulkDesignNode> designNodes, AxisRange[] axisRanges, BulkLoadArguments arguments)
    {
        var designNodeArray = designNodes.ToExistingOrNewArray();

        if (arguments.HasReachedLeafLevel)
        {
            // Finalize the design nodes into leaf nodes
            var nodes = designNodeArray.Select(node => node.FinalizeAsLeafNode(this)).ToArray();
            foreach (var node in nodes)
            {
                // Explicitly write the changes to the buffer, for the nodes will not be further processed
                node.WriteUponDestruction = false;
                node.WriteChangesToBuffer();

                AssertNodeProperlyWritten(node);
            }

            return;
        }

        var nextArguments = arguments.NextIteration();
        foreach (var designNode in designNodeArray)
        {
            var entryArray = designNode.GetAllEntriesArray(this);
            StableSortEntryArray(entryArray, axisRanges, arguments.CurrentDimension);
            var childrenDesignNodes = CreateDesignNodes(entryArray, arguments.ChildLevelNodeCount, designNode.ID);
            var finalized = designNode.FinalizeAsParentNode(this, childrenDesignNodes);

            // Then, for each formed slab, create a parent node representing it that will eventually contain nodes
            BulkLoadSlab(childrenDesignNodes, axisRanges, nextArguments);
        }
    }

    private record struct AxisRange(Axis Axis, double Min, double Max)
    {
        public double Range => Max - Min;

        public static AxisRange[] Ranges(Rectangle rectangle)
        {
            var ranges = new AxisRange[rectangle.Rank];
            for (int i = 0; i < ranges.Length; i++)
            {
                ref var range = ref ranges[i];

                range.Axis = (Axis)i;
                range.Min = rectangle.MinPoint.GetCoordinate(i);
                range.Max = rectangle.MaxPoint.GetCoordinate(i);
            }

            return ranges;
        }
        public static AxisRange[] Ranges(IEnumerable<Point> points)
        {
            return Ranges(Rectangle.CreateForPoints(points.ToArray()));
        }

        public sealed class RangeComparer : IComparer<AxisRange>
        {
            public static RangeComparer Instance { get; } = new();
            private RangeComparer() { }

            public int Compare(AxisRange left, AxisRange right)
            {
                return left.Range.CompareTo(right.Range);
            }
        }
    }

    private record struct BulkLoadArguments(int CurrentDimension, int Dimensionality, int TreeHeight, IReadOnlyList<int> LevelNodeCounts, int CurrentLevel)
    {
        public bool HasReachedLeafLevel => CurrentLevel >= TreeHeight - 1;

        public int CurrentLevelNodeCount => LevelNodeCounts[CurrentLevel];
        public int ChildLevelNodeCount => LevelNodeCounts[CurrentLevel + 1];

        public BulkLoadArguments NextIteration()
        {
            return this with
            {
                CurrentDimension = NextMod(CurrentDimension, Dimensionality),
                CurrentLevel = CurrentLevel + 1,
            };
        }

        public static BulkLoadArguments Initial(int dimensionality, int treeHeight, IReadOnlyList<int> levelNodeCounts)
        {
            return new(0, dimensionality, treeHeight, levelNodeCounts, 1);
        }

        private static int NextMod(int initial, int max)
        {
            return (initial + 1) % max;
        }
    }

    private ICollection<BulkDesignNode> CreateDesignNodes(TValue[] entryArray, int nodeCount, int parentID)
    {
        var chunks = entryArray.ToChunks(nodeCount).ToArray();

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
        var origin = Root!.Region.ExtremumPoint(dominatingExtremum);

        var nodeQueue = new PriorityQueue<EntryOrNode, double>();

        var result = new List<TValue>();

        while (nodeQueue.Count > 0)
        {
            var dequeued = nodeQueue.Dequeue();
            if (dequeued.IsNode)
            {
                var node = dequeued.Node.Value;
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
                        nodeQueue.Enqueue(child, child.Region.AbsoluteExtremumDistanceFrom(origin, dominatingExtremum));
                }
            }
            else
            {
                var entry = dequeued.Entry.Value;
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

    private IEnumerable<LeafNode> GetAllLeafs()
    {
        if (Root is null)
            return Enumerable.Empty<LeafNode>();

        if (Root is LeafNode leaf)
            return new[] { leaf };

        return GetAllLeafs((Root as ParentNode)!);
    }
    private IEnumerable<LeafNode> GetAllLeafs(ParentNode node)
    {
        // That is not funny
        var children = node.GetChildren().ToArray();

        foreach (var child in children)
        {
            if (child is LeafNode leaf)
            {
                yield return leaf;
            }
            else
            {
                var childLeafs = GetAllLeafs((child as ParentNode)!);
                foreach (var childLeaf in childLeafs)
                {
                    yield return childLeaf;
                }
            }
        }
    }

    public IEnumerable<TValue> NearestNeighborQuery(Point point, int neighbors)
    {
        if (point.Rank != Dimensionality)
            throw new InvalidOperationException("The given point's rank must match that of the contained entries'.");

        if (Root is null)
        {
            return Enumerable.Empty<TValue>();
        }

        if (neighbors >= GetEntryCount())
        {
            // The query is expensive anyway; at least the greater cost still remains in the IO
            return GetAllLeafs().SelectMany(l => l.GetEntries());
        }

        var nnHeap = new InMemory.MinHeap<NearestNeighborEntry>();

        if (Root is ParentNode parentRoot)
        {
            // This algorithm makes so much sense in my brain
            // I wonder if it's the one in the paper
            var children = parentRoot.GetChildren().ToArray();
            // Ascending distance from furthest rectangle vertex,
            // which defines the shortest sphere that contains at least one entire child node's rectangle
            children.SortBy((a, b) => a.Region.LargestDistanceFrom(point).CompareTo(b.Region.LargestDistanceFrom(point)));

            int currentLevel1Index = 0;

            Sphere searchSphere = default;
            int maxCurrentNodes = 0;
            // Keep increasing the sphere's radius until there is chance it covers the requested neighbor count
            do
            {
                ExpandCurrentSearchSphere();
            }
            while (maxCurrentNodes < neighbors);

            var nodeQueue = new PriorityQueue<Node, double>();
            for (int i = 0; i < currentLevel1Index; i++)
            {
                var node = children[i];
                AddNodeToPriorityQueue(node);
            }

            while (nodeQueue.Count > 0)
            {
                nodeQueue.TryDequeue(out var dequeued, out double distance);

                if (distance >= nnHeap.MaxValue.Distance)
                {
                    nodeQueue.Clear();
                    break;
                }

                switch (dequeued)
                {
                    case LeafNode leaf:
                        // Attempt to delay heap trimming by the most possible,
                        // in order to reduce the O(n) iterations that have to be performed
                        // In theory, this is an optimization
                        nnHeap.PreserveMaxEntryCount(neighbors);

                        var entries = leaf.GetEntries();
                        foreach (var entry in entries)
                        {
                            var entryDistance = entry.Location.DistanceFrom(point);
                            if (entryDistance > nnHeap.MaxValue.Distance)
                                continue;

                            nnHeap.Add(new NearestNeighborEntry(entry, distance));
                        }
                        break;

                    case ParentNode parent:
                        var parentChildren = parent.GetChildren();
                        AddNodesToPriorityQueue(parentChildren);
                        break;
                }
            }

            void AddNodesToPriorityQueue(IEnumerable<Node> nodes)
            {
                foreach (var node in nodes)
                    AddNodeToPriorityQueue(node);
            }

            void AddNodeToPriorityQueue(Node node)
            {
                double distance = node.Region.ShortestDistanceFrom(point);
                nodeQueue.Enqueue(node, distance);
            }

            void ExpandCurrentSearchSphere()
            {
                // Sanity check
                if (currentLevel1Index >= Order - 1)
                    return;

                searchSphere = GetForCurrentLevel1Index();
                AdvanceLevel1IndexCoveringCurrentSphere();
                maxCurrentNodes = (int)Math.Pow(MinChildren, Height) * currentLevel1Index;
            }
            Sphere GetForCurrentLevel1Index()
            {
                return new Sphere(point, children[currentLevel1Index].Region.LargestDistanceFrom(point));
            }
            void AdvanceLevel1IndexCoveringCurrentSphere()
            {
                while (searchSphere.Overlaps(children[currentLevel1Index].Region))
                {
                    currentLevel1Index++;
                }
            }

            return nnHeap.Select(e => e.Entry);
        }
        else if (Root is LeafNode leafRoot)
        {
            var entries = leafRoot.GetEntries().ToArray();

            // At this point it is guaranteed that the requested neighbors are less than the total entries
            var ascendingDistanceComparer = new ILocated.ClosestDistanceComparer<TValue>(point);
            return entries.SortBy(ascendingDistanceComparer).Take(neighbors);
        }

        throw new InvalidOperationException("The root is not a valid node type and could not be handled.");
    }

    private record struct NearestNeighborEntry(TValue Entry, double Distance) : IComparable<NearestNeighborEntry>
    {
        public static NearestNeighborEntry FromDistance(TValue value, Point center)
        {
            return new(value, value.Location.DifferenceFrom(center).DistanceFromCenter);
        }

        public int CompareTo(RStarTree<TValue>.NearestNeighborEntry other)
        {
            return Distance.CompareTo(other.Distance);
        }
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
        nodeQueue.Enqueue(Root!);

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
        if (Root is null)
            return true;

        if (!Root.IsRoot)
            return false;

        return VerifyIntegrity(Root);
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
                    if (child.IsRoot)
                        return false;

                    bool contained = parent.Region.Contains(child.Region, true);
                    if (!contained)
                        return false;

                    bool sane = VerifyIntegrity(child);
                    if (!sane)
                        return false;
                }
                break;

            case LeafNode leaf:
                foreach (var entry in leaf.GetEntries())
                {
                    Debug.Assert(!entry.Location.IsInvalid, $"The entry with ID {entry.ID} in leaf node with ID {leaf.ID} had an invalid location.");

                    bool contained = leaf.Region.Contains(entry.Location, true);
                    if (!contained)
                        return false;
                }
                break;

            // Unexpected node type
            default:
                return false;
        }

        if (node.ShouldMerge || node.ShouldSplit)
            return false;

        return true;
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

        public TValue[] GetAllEntriesArray(RStarTree<TValue> tree)
        {
            return EntryIDs.Select(tree.GetEntry).ToArray();
        }
    }

    private void InitializeRoot(TValue value)
    {
        Root = AllocateNewLeafRootNode(Enumerable.Empty<int>(), Rectangle.FromSinglePoint(value.Location));
        NodeCount = 1;
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

    private LeafNode AllocateNewLeafNode(int parentID, IEnumerable<int> childrenIDs, Rectangle region)
    {
        int newID = AllocateNextID();
        return new LeafNode(this, newID, parentID, childrenIDs, region);
    }
    private ParentNode AllocateNewParentNode(int parentID, IEnumerable<int> childrenIDs, Rectangle region)
    {
        int newID = AllocateNextID();
        return new ParentNode(this, newID, parentID, childrenIDs, region);
    }

    // These methods assume that RootID has been allocated beforehand
    // This could be improved in the ID allocator such that RootID is always assumed allocated
    // There is not enough time to mess with that
    private LeafNode AllocateNewLeafRootNode(IEnumerable<int> childrenIDs, Rectangle region)
    {
        return new LeafNode(this, NodeID.Root, NodeID.Null, childrenIDs, region);
    }
    private ParentNode AllocateNewParentRootNode(IEnumerable<int> childrenIDs, Rectangle region)
    {
        return new ParentNode(this, NodeID.Root, NodeID.Null, childrenIDs, region);
    }

    internal delegate TNode NewNodeAllocator<TNode>(int parentID, IEnumerable<int> childrenIDs, Rectangle region)
        where TNode : Node;

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

    public abstract class Node : IID
    {
        protected internal SortedSet<int> ChildrenIDs;

        public RStarTree<TValue> Tree { get; }

        public virtual Rectangle Region { get; protected set; }

        public abstract NodeType NodeType { get; }

        public bool WriteUponDestruction { get; set; } = true;

        int IID.ID
        {
            get => ID;
            set => throw null!;
        }

        public int ID { get; internal set; }
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

        public bool ShouldMerge => !IsRoot && ChildrenIDs.Count < Tree.MinChildren;
        public bool ShouldSplit => ChildrenIDs.Count > Tree.MaxChildren;

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

        public LazyNode? GetLazyParent()
        {
            if (ParentID.IsNull)
                return null;

            return Tree.GetLazyNode(ParentID);
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

        public void ExpandRegion(Point newPoint)
        {
            var newRegion = Region.Expand(newPoint);
            PropagateExpansion(newRegion);
        }
        public void ExpandRegion(Rectangle newRectangle)
        {
            var newRegion = Region.Expand(newRectangle);
            PropagateExpansion(newRegion);
        }

        private void PropagateExpansion(Rectangle newRegion)
        {
            if (Region == newRegion)
                return;

            Region = newRegion;

            var lazyParent = GetLazyParent();
            if (lazyParent is null)
                return;

            lazyParent.ExpandRegion(newRegion);
        }

        public abstract IEnumerable<Rectangle> GetChildrenRectangles();

        public abstract void RefreshMBR();

        /// <summary>Trims the region of this node and creates a new node containing the excluded children.</summary>
        /// <param name="newRegion">The new region of this node. It cannot be a superset of the current region.</param>
        /// <returns>The newly created node containing the excluded children after this node's trimming.</returns>
        public abstract Node TrimRegion(Rectangle newRegion);

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
            if (Region.IsInvalid)
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

        private protected TNode TrimRegion<TChildren, TNode>(Rectangle newRegion, Func<IEnumerable<TChildren>> childrenRetriever, Predicate<TChildren> childrenPredicate, NewNodeAllocator<TNode> allocator)
            where TChildren : IID
            where TNode : Node
        {
            var children = childrenRetriever();
            children.Dissect(childrenPredicate, out var inside, out var outside);
            // Locally cache the values
            inside = inside.ToList(ChildrenCount);
            outside = outside.ToList(ChildrenCount);
            Region = newRegion;

            ChildrenIDs = new(inside.Select(c => c.ID));
            return allocator(ParentID, outside.Select(c => c.ID), newRegion);
        }

        public int GetLevel()
        {
            int level = 0;
            var current = this;
            while (true)
            {
                var parent = current.GetLazyParent();
                if (parent is null)
                    break;
                current = parent;
            }
            return level;
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

                return region!.Value;
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

        public override void RefreshMBR()
        {
            GetFullyLoadedNode().RefreshMBR();
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
            WriteOrSkipRegion(ref writer);
            WriteTypeAndChildrenCount(ref writer);
            WriteOrSkipChildrenIDs(ref writer);

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

        public override void RefreshMBR()
        {
            var children = GetChildren();
            Region = Rectangle.CreateForRectangles(children.Select(e => e.Region).ToArray());
        }

        public override ParentNode TrimRegion(Rectangle newRegion)
        {
            return TrimRegion(newRegion, GetChildren, ChildrenPredicate, Tree.AllocateNewParentNode);

            bool ChildrenPredicate(Node child)
            {
                return newRegion.Contains(child.Region);
            }
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

                double overlap = customRegion.OverlappingVolume(otherChild.Region);
                totalOverlap += overlap;
            }
            return totalOverlap;
        }

        public IEnumerable<Node> ChildrenContaining(Point point)
        {
            return GetLazyChildren().Where(child => child.Region.Contains(point, true));
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
            ExpandRegion(childNode.Region);
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

        public override void RefreshMBR()
        {
            var entries = GetEntries();
            Region = Rectangle.CreateForPoints(entries.Select(e => e.Location).ToArray());
        }

        public override LeafNode TrimRegion(Rectangle newRegion)
        {
            return TrimRegion(newRegion, GetEntries, ChildrenPredicate, Tree.AllocateNewLeafNode);

            bool ChildrenPredicate(TValue entry)
            {
                return newRegion.Contains(entry.Location);
            }
        }

        public IEnumerable<TValue> GetEntries() => ChildrenIDs.Select(Tree.GetEntry);

        public TValue? ValueAt(Point point) => GetEntries().FirstOrDefault(entry => entry.Location == point);

        public void AddEntry(TValue entry)
        {
            ChildrenIDs.Add(entry.ID);
            ExpandRegion(entry.Location);
        }

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
        public const int RootID = 1;
        private const int nullID = 0;

        public static readonly NodeID Null = new(nullID);
        public static readonly NodeID Root = new(RootID);

        public bool IsNull => ID <= nullID;
        public bool IsRoot => ID is RootID;

        public static implicit operator NodeID(int id) => new(id);
        public static implicit operator int(NodeID id) => id.ID;
    }

    public enum NodeType
    {
        Unknown = -1,

        Parent = 0,
        Leaf,
    }

    private class OperationReferenceFactory
    {
        private object? current;

        public void DisposeCurrent()
        {
            current = null;
        }
        public object ForceNext()
        {
            return current = new();
        }
        public object CurrentOrNext()
        {
            return current ?? ForceNext();
        }
    }
}
