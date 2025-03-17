using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Transfer;

/// <summary>
/// A stream wrapper that throttles the bandwidth to a specified rate.
/// </summary>
public class ThrottledStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _bytesPerSecond;
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private long _totalBytesRead;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottledStream"/> class.
    /// </summary>
    /// <param name="baseStream">The underlying stream to throttle.</param>
    /// <param name="bytesPerSecond">The maximum number of bytes per second to allow.</param>
    public ThrottledStream(Stream baseStream, long bytesPerSecond)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _bytesPerSecond = bytesPerSecond > 0 ? bytesPerSecond : throw new ArgumentOutOfRangeException(nameof(bytesPerSecond), "Bytes per second must be greater than zero.");
        _stopwatch.Start();
    }
    
    /// <inheritdoc />
    public override bool CanRead => _baseStream.CanRead;
    
    /// <inheritdoc />
    public override bool CanSeek => _baseStream.CanSeek;
    
    /// <inheritdoc />
    public override bool CanWrite => _baseStream.CanWrite;
    
    /// <inheritdoc />
    public override long Length => _baseStream.Length;
    
    /// <inheritdoc />
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }
    
    /// <inheritdoc />
    public override void Flush()
    {
        _baseStream.Flush();
    }
    
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        // Read in small chunks to maintain the throttle rate
        const int chunkSize = 8192; // 8KB chunks
        int totalBytesRead = 0;
        
        while (totalBytesRead < count)
        {
            int bytesToRead = Math.Min(chunkSize, count - totalBytesRead);
            
            // Throttle before reading
            ThrottleTransfer(bytesToRead);
            
            // Read the chunk
            int bytesRead = _baseStream.Read(buffer, offset + totalBytesRead, bytesToRead);
            if (bytesRead == 0)
                break; // End of stream
                
            totalBytesRead += bytesRead;
            _totalBytesRead += bytesRead;
        }
        
        return totalBytesRead;
    }
    
    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Read in small chunks to maintain the throttle rate
        const int chunkSize = 8192; // 8KB chunks
        int totalBytesRead = 0;
        
        while (totalBytesRead < count)
        {
            int bytesToRead = Math.Min(chunkSize, count - totalBytesRead);
            
            // Throttle before reading
            await ThrottleTransferAsync(bytesToRead, cancellationToken);
            
            // Read the chunk
            int bytesRead = await _baseStream.ReadAsync(buffer, offset + totalBytesRead, bytesToRead, cancellationToken);
            if (bytesRead == 0)
                break; // End of stream
                
            totalBytesRead += bytesRead;
            _totalBytesRead += bytesRead;
        }
        
        return totalBytesRead;
    }
    
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        return _baseStream.Seek(offset, origin);
    }
    
    /// <inheritdoc />
    public override void SetLength(long value)
    {
        _baseStream.SetLength(value);
    }
    
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        // Write in small chunks to maintain the throttle rate
        const int chunkSize = 8192; // 8KB chunks
        int totalBytesWritten = 0;
        
        while (totalBytesWritten < count)
        {
            int bytesToWrite = Math.Min(chunkSize, count - totalBytesWritten);
            
            // Throttle before writing
            ThrottleTransfer(bytesToWrite);
            
            // Write the chunk
            _baseStream.Write(buffer, offset + totalBytesWritten, bytesToWrite);
            totalBytesWritten += bytesToWrite;
            _totalBytesRead += bytesToWrite; // Use the same counter for simplicity
        }
    }
    
    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Write in small chunks to maintain the throttle rate
        const int chunkSize = 8192; // 8KB chunks
        int totalBytesWritten = 0;
        
        while (totalBytesWritten < count)
        {
            int bytesToWrite = Math.Min(chunkSize, count - totalBytesWritten);
            
            // Throttle before writing
            await ThrottleTransferAsync(bytesToWrite, cancellationToken);
            
            // Write the chunk
            await _baseStream.WriteAsync(buffer, offset + totalBytesWritten, bytesToWrite, cancellationToken);
            totalBytesWritten += bytesToWrite;
            _totalBytesRead += bytesToWrite; // Use the same counter for simplicity
        }
    }
    
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream.Dispose();
            _stopwatch.Stop();
        }
        
        base.Dispose(disposing);
    }
    
    private void ThrottleTransfer(int byteCount)
    {
        if (byteCount <= 0)
            return;
            
        // Calculate how long this transfer should take at the throttled rate
        double expectedSeconds = (double)byteCount / _bytesPerSecond;
        int expectedMs = (int)(expectedSeconds * 1000);
        
        // Force a minimum delay for each chunk to maintain the rate
        if (expectedMs > 0)
        {
            Thread.Sleep(expectedMs);
        }
    }
    
    private async Task ThrottleTransferAsync(int byteCount, CancellationToken cancellationToken)
    {
        if (byteCount <= 0)
            return;
            
        // Calculate how long this transfer should take at the throttled rate
        double expectedSeconds = (double)byteCount / _bytesPerSecond;
        int expectedMs = (int)(expectedSeconds * 1000);
        
        // Force a minimum delay for each chunk to maintain the rate
        if (expectedMs > 0)
        {
            await Task.Delay(expectedMs, cancellationToken);
        }
    }
} 