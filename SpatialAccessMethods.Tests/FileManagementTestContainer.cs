using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using UnitsNet;

namespace SpatialAccessMethods.Tests;

public abstract class FileManagementTestContainer
{
    protected static MinHeap<T> CreateMinHeap<T>(Information blockSize, MasterBufferController masterController)
        where T : unmanaged, INumber<T>
    {
        var memoryStream = new MemoryStream(blockSize.BytesInt32());
        var bufferController = new ChildBufferController(memoryStream, masterController)
        {
            BlockSize = blockSize
        };
        return new MinHeap<T>(bufferController);
    }
}
