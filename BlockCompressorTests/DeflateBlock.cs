namespace BlockCompressorTests;

public class DeflateBlock
{
    public required byte[] CompressedData { get; init; }
    public required byte[] OriginalData { get; init; }
}