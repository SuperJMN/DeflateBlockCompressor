using System.Reactive.Linq;
using BlockCompressor;
using Zafiro.Mixins;
using Zafiro.Reactive;

namespace BlockCompressorTests;

public class MsixCompressionTest
{
    [Fact]
    public async Task Test()
    {
        await using var file = File.OpenRead("HelloWorld.dat");

        var blocks = await Compressed.Blocks(file.ToObservable().Flatten()).ToList();
        var compressedBytes = blocks.Select(x => x.CompressedData).Flatten().ToArray();

        var originalBytes = DeflateHelper.DecompressDeflateData(compressedBytes);
        Assert.True((await File.ReadAllBytesAsync("HelloWorld.dat")).SequenceEqual(originalBytes));
    }
}