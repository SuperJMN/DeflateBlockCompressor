using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ICSharpCode.SharpZipLib.Zip.Compression;
namespace BlockCompressorTests;
public static class DeflateBlockCompressor
{
    /// <summary>
    /// Creates an observable sequence of deflate blocks from an input byte stream.
    /// </summary>
    /// <param name="input">Input byte stream to compress</param>
    /// <param name="maybeDeflater">Optional deflater instance</param>
    /// <param name="uncompressedBlockSize">Size of uncompressed blocks (default: 64KB)</param>
    /// <returns>Observable sequence of deflate blocks</returns>
    public static IObservable<DeflateBlock> Blocks(IObservable<byte> input, Maybe<Deflater> maybeDeflater,
        int uncompressedBlockSize = 64 * 1024)
    {
        var deflater = maybeDeflater.GetValueOrDefault(new Deflater(8, true));
        return Observable.Create<DeflateBlock>(observer => 
        {
            var buffer = new List<byte>();
            var subscription = input.Subscribe(
                onNext: b => 
                {
                    buffer.Add(b);
                    if (buffer.Count >= uncompressedBlockSize)
                    {
                        // When buffer reaches the block size, compress the block and remove processed bytes
                        observer.OnNext(CompressBlock(buffer.Take(uncompressedBlockSize).ToList(), deflater, false));
                        buffer.RemoveRange(0, uncompressedBlockSize);
                    }
                },
                onError: observer.OnError,
                onCompleted: () => 
                {
                    if (buffer.Count > 0)
                    {
                        // Process remaining bytes as the last block when input completes
                        observer.OnNext(CompressBlock(buffer, deflater, true));
                    }
                    observer.OnCompleted();
                }
            );
        
            return subscription;
        });
    }
    
    /// <summary>
    /// Compresses a block of bytes using the provided deflater
    /// </summary>
    /// <param name="uncompressedBlock">The block of bytes to compress</param>
    /// <param name="deflater">The deflater instance to use</param>
    /// <param name="isLastBlock">Whether this is the final block in the sequence</param>
    /// <returns>A DeflateBlock containing both compressed and original data</returns>
    private static DeflateBlock CompressBlock(IList<byte> uncompressedBlock, Deflater deflater, bool isLastBlock)
    {
        var input = uncompressedBlock.ToArray();
        deflater.SetInput(input);

        using var compressedStream = new MemoryStream();
        byte[] buffer = new byte[4096];

        if (isLastBlock)
        {
            deflater.Finish();
            int bytesCompressed;
            while (!deflater.IsFinished && (bytesCompressed = deflater.Deflate(buffer)) > 0)
            {
                compressedStream.Write(buffer, 0, bytesCompressed);
            }
        }
        else
        {
            deflater.Flush();
            int bytesCompressed;
            while (!deflater.IsNeedingInput && (bytesCompressed = deflater.Deflate(buffer)) > 0)
            {
                compressedStream.Write(buffer, 0, bytesCompressed);
            }
        }

        return new DeflateBlock
        {
            CompressedData = compressedStream.ToArray(),
            OriginalData = input
        };
    }
}