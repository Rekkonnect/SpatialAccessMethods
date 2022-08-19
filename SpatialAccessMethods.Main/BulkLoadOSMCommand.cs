using Garyon.Extensions;
using SpatialAccessMethods.FileManagement;
using System.Xml;

namespace SpatialAccessMethods.Main;

public sealed class BulkLoadOSMCommand : FailableConsoleCommand
{
    public string? FileLocation { get; set; }

    public BulkLoadOSMCommand()
    {
        IsCommand("bulkload", "Bulk load the database from a file");

        HasLongDescription(
"""
Loads the contents of an .osm file and attempts to bulk load
the entries to the database.

Usage example: `bulkload -f "/path/to/file.osm"`
""");

        HasRequiredOption("f|file=", "The full path of the file", p => FileLocation = p);
    }

    protected override int RunUnsafe(string[] remainingArguments)
    {
        var document = new XmlDocument();
        document.Load(FileLocation!);
        var entries = GetEntries(document).ToArray();
        DatabaseController.Instance.Table.BulkLoad(entries);
        
        return Success;
    }

    private static IEnumerable<MapRecordEntry> GetEntries(XmlDocument document)
    {
        return GetEntryNodes(document).Select(ParseFromNode);
    }
    private static IEnumerable<XmlNode> GetEntryNodes(XmlDocument document)
    {
        var nodes = document["osm"]!.ChildNodes.Cast<XmlNode>();
        return nodes.Where(node => node.Name is "node");
    }
    private static MapRecordEntry ParseFromNode(XmlNode node)
    {
        // Intentional float => double 
        double latitude = ParseCoordinate(node, "lat");
        double longitude = ParseCoordinate(node, "lon");
        var location = new Point(new[] { latitude, longitude });

        var tagNodes = node.ChildNodes.Cast<XmlElement>();
        var nameNode = tagNodes.FirstOrDefault(node => node.Attributes["k"]!.Value is "name");
        string? name = nameNode?.Attributes["v"]!.Value;

        return new(location, name);
    }
    private static double ParseCoordinate(XmlNode node, string coordinateAttributeName)
    {
        return (double)node.Attributes![coordinateAttributeName]!.Value.ParseSingle();
    }
}
