using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using UnitsNet;

namespace SpatialAccessMethods.QualityAssurance;

public abstract class FileManagementQAContainer
{
    protected static MinHeap<T> CreateMinHeap<T>(Information blockSize, MasterBufferController masterController)
        where T : unmanaged, INumber<T>
    {
        var memoryStream = CreateMemoryStream(blockSize.BytesInt32());
        var bufferController = new ChildBufferController(memoryStream, masterController)
        {
            BlockSize = blockSize
        };
        return new MinHeap<T>(bufferController);
    }

    protected static MemoryStream CreateMemoryStream(int bytes)
    {
        return new(bytes);
    }
}
