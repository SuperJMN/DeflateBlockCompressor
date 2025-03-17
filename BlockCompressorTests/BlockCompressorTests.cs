using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Zafiro.Mixins;

namespace BlockCompressorTests
{
    public class BlockCompressorTests
    {
        [Fact]
        public async Task Test_blocks_can_be_deflated()
        {
            var originalBytes = await File.ReadAllBytesAsync("Sample.bmp");
            var originalContent = originalBytes.ToObservable();
            var blocks = await DeflateBlockCompressor.Blocks(originalContent, Maybe<Deflater>.None).ToList();

            foreach (var block in blocks)
            {
                var uncompressed = await block.Decompress();
                Assert.True(uncompressed.SequenceEqual(block.OriginalData));
            }
        }

        [Fact]
        public async Task Test_concat_can_be_deflated()
        {
            var originalBytes = await File.ReadAllBytesAsync("Sample.bmp");
            var originalContent = originalBytes.ToObservable();
            var blocks = await DeflateBlockCompressor.Blocks(originalContent, Maybe<Deflater>.None).ToList();
            var blockBytes = blocks.Select(x => x.CompressedData).Flatten().ToArray();

            Assert.True(originalBytes.SequenceEqual(await Decompress(blockBytes)));
        }

        private static async Task<byte[]> Decompress(byte[] bytes)
        {
            var decompressed = new MemoryStream();
            using (var compressedStream = new MemoryStream(bytes))
            using (var inflater = new ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream(compressedStream))
            {
                await inflater.CopyToAsync(decompressed);
                return decompressed.ToArray();
            }
        }
    }
}