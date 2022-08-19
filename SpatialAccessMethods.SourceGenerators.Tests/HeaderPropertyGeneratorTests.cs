using NUnit.Framework;
using RoseLynn.Generators;

namespace SpatialAccessMethods.SourceGenerators.Tests;

public sealed class HeaderPropertyGeneratorTests : BaseSourceGeneratorTestContainer<HeaderPropertyGenerator>
{
    [Test]
    [Ignore("Got so far as to validate that the generated code is correct, didn't bother setting up the final diagnostics about missing preview references.")]
    public void SimpleCase()
    {
        var source = """
                     using SpatialAccessMethods.FileManagement;
                     
                     namespace Example;
                     
                     [HeaderProperty<int>("ByteCount")]
                     [HeaderProperty<long, int>("StreamPosition")]
                     [HeaderProperty<int, byte>("Byte")]
                     public partial struct SampleHeaderBlock : IHeaderBlock
                     {
                         // Header information
                         public RawData Data { get; set; }
                     }
                     """;

        var expected =  """
                        using SpatialAccessMethods.FileManagement;
                        
                        namespace Example;

                        partial struct SampleHeaderBlock
                        {
                            public int ByteCount
                            {
                                get => (this as IHeaderBlock).GetHeaderProperty<int>(0);
                                set => (this as IHeaderBlock).SetHeaderProperty(0, value);
                            }
                            public long StreamPosition
                            {
                                get => (long)(this as IHeaderBlock).GetHeaderProperty<int>(4);
                                set => (this as IHeaderBlock).SetHeaderProperty(4, (int)value);
                            }
                            public int Byte
                            {
                                get => (int)(this as IHeaderBlock).GetHeaderProperty<byte>(8);
                                set => (this as IHeaderBlock).SetHeaderProperty(8, (byte)value);
                            }
                        }
                        
                        """;
        
        var mappings = new GeneratedSourceMappings();
        mappings.Add("SampleHeaderBlock.HeaderProperties.g.cs", expected);

        VerifyAsync(source, mappings).Wait();
    }
}
