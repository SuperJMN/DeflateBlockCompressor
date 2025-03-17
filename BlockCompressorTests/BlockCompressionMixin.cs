using ICSharpCode.SharpZipLib.Zip.Compression;

namespace BlockCompressorTests;

public static class BlockCompressionMixin
{
    // Método para descomprimir este bloque individualmente
    public static async Task<byte[]> Decompress(this DeflateBlock block)
    {
        var deflater = new Deflater();
        deflater.SetInput(block.OriginalData);
        deflater.Finish();
        
        using var tempStream = new MemoryStream();
        byte[] buffer = new byte[4096];
        int bytesCompressed;
        
        while (!deflater.IsFinished && (bytesCompressed = deflater.Deflate(buffer)) > 0)
        {
            tempStream.Write(buffer, 0, bytesCompressed);
        }
        
        // Descomprimir el bloque bien formado
        var decompressed = new MemoryStream();
        using (var compressedStream = new MemoryStream(tempStream.ToArray()))
        using (var inflater = new ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream(compressedStream))
        {
            await inflater.CopyToAsync(decompressed);
            return decompressed.ToArray();
        }
    }

}