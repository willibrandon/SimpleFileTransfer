using System.IO;
using System.IO.Compression;

namespace SimpleFileTransfer;

/// <summary>
/// Provides utility methods for compressing and decompressing data using GZip compression.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Compresses a stream using GZip compression.
    /// </summary>
    /// <param name="source">The source stream to compress.</param>
    /// <param name="destination">The destination stream to write compressed data to.</param>
    public static void Compress(Stream source, Stream destination)
    {
        using var compressionStream = new GZipStream(destination, CompressionMode.Compress, true);
        source.CopyTo(compressionStream);
    }

    /// <summary>
    /// Decompresses a stream using GZip decompression.
    /// </summary>
    /// <param name="source">The source stream containing compressed data.</param>
    /// <param name="destination">The destination stream to write decompressed data to.</param>
    public static void Decompress(Stream source, Stream destination)
    {
        using var decompressionStream = new GZipStream(source, CompressionMode.Decompress, true);
        decompressionStream.CopyTo(destination);
    }

    /// <summary>
    /// Gets the compression ratio as a percentage.
    /// </summary>
    /// <param name="originalSize">The original size in bytes.</param>
    /// <param name="compressedSize">The compressed size in bytes.</param>
    /// <returns>The compression ratio as a percentage.</returns>
    public static double GetCompressionRatio(long originalSize, long compressedSize)
    {
        if (originalSize == 0)
            return 0;
            
        return 100.0 - ((double)compressedSize / originalSize * 100.0);
    }
}
