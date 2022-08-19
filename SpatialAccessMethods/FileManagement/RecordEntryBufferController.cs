using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.FileManagement;

public class RecordEntryBufferController : EntryBufferController
{
    // Header block; NOT handled by the master buffer controller
    public DataHeaderBlock HeaderBlock;

    public override Information BlockSize
    {
        get
        {
            if (HeaderBlock.IsDefault)
                return 32.Kibibytes();
            
            return HeaderBlock.BlockSize;
        }
    }

    protected override Information EntrySize => 256.Bytes();

    public override int MandatoryBlocks => 1;

    public RecordEntryBufferController(Stream stream, MasterBufferController masterController)
        : base(stream, masterController)
    {
        HeaderBlock = LoadUnconstrained(0);
    }
    public RecordEntryBufferController(string blockFilePath, MasterBufferController masterController)
        : base(blockFilePath, masterController)
    {
        HeaderBlock = LoadUnconstrained(0);
    }

    public override void Dispose()
    {
        Dump(HeaderBlock);
        base.Dispose();
    }

    protected override int GetBlockID(int entryID, out int spanStart)
    {
        return base.GetBlockID(entryID, out spanStart) + 1;
    }
}
