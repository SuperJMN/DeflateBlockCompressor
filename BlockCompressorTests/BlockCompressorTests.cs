using System.IO.Compression;
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
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

            var decompressIoCompression = await DecompressWithMicrosoft(blockBytes);
            Assert.True(originalBytes.SequenceEqual(decompressIoCompression));
        }

        [Fact]
        public async Task Test_concat_can_be_deflated_with_string()
        {
            var originalBytes = "HOLA"u8.ToArray();
            var originalContent = originalBytes.ToObservable();
            var blocks = await DeflateBlockCompressor.Blocks(originalContent, Maybe<Deflater>.None).ToList();
            var blockBytes = blocks.Select(x => x.CompressedData).Flatten().ToArray();

            Assert.True(originalBytes.SequenceEqual(await DecompressWithMicrosoft(blockBytes)));
        }

        private static async Task<byte[]> DecompressWithSharpZipLib(byte[] bytes)
        {
            var decompressed = new MemoryStream();
            using (var compressedStream = new MemoryStream(bytes))
            using (var inflater = new InflaterInputStream(compressedStream))
            {
                await inflater.CopyToAsync(decompressed);
                return decompressed.ToArray();
            }
        }

        private static async Task<byte[]> DecompressWithMicrosoft(byte[] bytes)
        {
            var decompressed = new MemoryStream();
            using (var compressedStream = new MemoryStream(bytes))
            using (var inflater = new System.IO.Compression.DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                await inflater.CopyToAsync(decompressed);
                return decompressed.ToArray();
            }
        }
    }
}