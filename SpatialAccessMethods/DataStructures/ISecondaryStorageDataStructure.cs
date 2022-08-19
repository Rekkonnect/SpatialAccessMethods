using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.DataStructures;

public interface ISecondaryStorageDataStructure
{
    public ChildBufferController BufferController { get; }
}
