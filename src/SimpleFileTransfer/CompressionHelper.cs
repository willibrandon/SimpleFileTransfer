using System.IO;
using System.IO.Compression;

namespace SimpleFileTransfer;

/// <summary>
/// Provides utility methods for compressing and decompressing data using different compression algorithms.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Specifies the compression algorithm to use.
    /// </summary>
    public enum CompressionAlgorithm
    {
        /// <summary>
        /// GZip compression algorithm.
        /// </summary>
        GZip,
        
        /// <summary>
        /// Brotli compression algorithm.
        /// </summary>
        Brotli
    }
    
    /// <summary>
    /// Compresses a stream using the specified compression algorithm.
    /// </summary>
    /// <param name="source">The source stream to compress.</param>
    /// <param name="destination">The destination stream to write compressed data to.</param>
    /// <param name="algorithm">The compression algorithm to use. Defaults to GZip.</param>
    public static void Compress(Stream source, Stream destination, CompressionAlgorithm algorithm = CompressionAlgorithm.GZip)
    {
        switch (algorithm)
        {
            case CompressionAlgorithm.Brotli:
                using (var compressionStream = new BrotliStream(destination, CompressionMode.Compress, true))
                {
                    source.CopyTo(compressionStream);
                }
                break;
                
            case CompressionAlgorithm.GZip:
            default:
                using (var compressionStream = new GZipStream(destination, CompressionMode.Compress, true))
                {
                    source.CopyTo(compressionStream);
                }
                break;
        }
    }

    /// <summary>
    /// Decompresses a stream using the specified compression algorithm.
    /// </summary>
    /// <param name="source">The source stream containing compressed data.</param>
    /// <param name="destination">The destination stream to write decompressed data to.</param>
    /// <param name="algorithm">The compression algorithm to use. Defaults to GZip.</param>
    public static void Decompress(Stream source, Stream destination, CompressionAlgorithm algorithm = CompressionAlgorithm.GZip)
    {
        switch (algorithm)
        {
            case CompressionAlgorithm.Brotli:
                using (var decompressionStream = new BrotliStream(source, CompressionMode.Decompress, true))
                {
                    decompressionStream.CopyTo(destination);
                }
                break;
                
            case CompressionAlgorithm.GZip:
            default:
                using (var decompressionStream = new GZipStream(source, CompressionMode.Decompress, true))
                {
                    decompressionStream.CopyTo(destination);
                }
                break;
        }
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
