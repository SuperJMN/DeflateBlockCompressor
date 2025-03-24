using System.Reactive.Linq;
using ZLibDotNet;

namespace BlockCompressor;

public static class Compressed
{
    /// <summary>
    /// Default block size (64KB)
    /// </summary>
    public const int DEFAULT_BLOCK_SIZE = 64 * 1024; // 64KB
    /// <summary>
    /// Compresses an observable stream of bytes into blocks using zlib's deflate algorithm
    /// configured in the style of makemsix/makeappx
    /// </summary>
    public static IObservable<DeflateBlock> Blocks(
        IObservable<byte> input,
        int compressionLevel = ZLib.Z_BEST_COMPRESSION,
        int uncompressedBlockSize = DEFAULT_BLOCK_SIZE)
    {
        return Observable.Create<DeflateBlock>(observer =>
        {
            var buffer = new List<byte>();
            var wrapper = new ZStreamWrapper();
            var subscription = input.Subscribe(
                onNext: b =>
                {
                    buffer.Add(b);
                    if (buffer.Count >= uncompressedBlockSize)
                    {
                        ProcessBlock(buffer.ToArray());
                        buffer.Clear();
                    }
                },
                onError: observer.OnError,
                onCompleted: () =>
                {
                    if (buffer.Count > 0)
                    {
                        ProcessBlock(buffer.ToArray());
                        buffer.Clear();
                    }
                    // Process the final block
                    byte[] finalData = wrapper.DeflateFinish();
                    
                    if (finalData.Length > 0)
                    {
                        var finalBlock = new DeflateBlock
                        {
                            CompressedData = finalData,
                            OriginalData = Array.Empty<byte>(),
                        };
                        
                        observer.OnNext(finalBlock);
                    }
                    observer.OnCompleted();
                });
            // Local function to process a block
            void ProcessBlock(byte[] blockData)
            {
                try
                {
                    // Compress the block using our wrapper
                    byte[] compressedData = wrapper.Deflate(
                        blockData, 
                        compressionLevel);
                        
                    // Create and emit the compressed block
                    var block = new DeflateBlock
                    {
                        CompressedData = compressedData,
                        OriginalData = blockData,
                    };
                    observer.OnNext(block);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            }
            return subscription;
        });
    }
}