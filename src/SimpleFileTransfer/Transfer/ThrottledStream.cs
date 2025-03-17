using System;
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
    private readonly DateTime _startTime;
    private long _bytesTransferred;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottledStream"/> class.
    /// </summary>
    /// <param name="baseStream">The underlying stream to throttle.</param>
    /// <param name="bytesPerSecond">The maximum number of bytes per second to allow.</param>
    public ThrottledStream(Stream baseStream, long bytesPerSecond)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _bytesPerSecond = bytesPerSecond > 0 ? bytesPerSecond : throw new ArgumentOutOfRangeException(nameof(bytesPerSecond), "Bytes per second must be greater than zero.");
        _startTime = DateTime.UtcNow;
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
        Throttle(count);
        int bytesRead = _baseStream.Read(buffer, offset, count);
        _bytesTransferred += bytesRead;
        return bytesRead;
    }
    
    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await ThrottleAsync(count, cancellationToken);
        int bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        _bytesTransferred += bytesRead;
        return bytesRead;
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
        Throttle(count);
        _baseStream.Write(buffer, offset, count);
        _bytesTransferred += count;
    }
    
    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await ThrottleAsync(count, cancellationToken);
        await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        _bytesTransferred += count;
    }
    
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream.Dispose();
        }
        
        base.Dispose(disposing);
    }
    
    private void Throttle(int count)
    {
        if (count <= 0)
            return;
            
        // Calculate how long the operation should take at the throttled rate
        double expectedSeconds = (double)count / _bytesPerSecond;
        long expectedMilliseconds = (long)(expectedSeconds * 1000);
        
        // Calculate how much time has elapsed since the start
        TimeSpan elapsed = DateTime.UtcNow - _startTime;
        long elapsedMilliseconds = (long)elapsed.TotalMilliseconds;
        
        // Calculate how many bytes we should have transferred in the elapsed time
        long expectedBytes = (long)(_bytesPerSecond * (elapsedMilliseconds / 1000.0));
        
        // If we've transferred more bytes than expected, sleep to throttle
        if (_bytesTransferred > expectedBytes)
        {
            long excessBytes = _bytesTransferred - expectedBytes;
            long sleepMilliseconds = (long)((double)excessBytes / _bytesPerSecond * 1000);
            
            if (sleepMilliseconds > 0)
            {
                Thread.Sleep((int)sleepMilliseconds);
            }
        }
    }
    
    private async Task ThrottleAsync(int count, CancellationToken cancellationToken)
    {
        if (count <= 0)
            return;
            
        // Calculate how long the operation should take at the throttled rate
        double expectedSeconds = (double)count / _bytesPerSecond;
        long expectedMilliseconds = (long)(expectedSeconds * 1000);
        
        // Calculate how much time has elapsed since the start
        TimeSpan elapsed = DateTime.UtcNow - _startTime;
        long elapsedMilliseconds = (long)elapsed.TotalMilliseconds;
        
        // Calculate how many bytes we should have transferred in the elapsed time
        long expectedBytes = (long)(_bytesPerSecond * (elapsedMilliseconds / 1000.0));
        
        // If we've transferred more bytes than expected, sleep to throttle
        if (_bytesTransferred > expectedBytes)
        {
            long excessBytes = _bytesTransferred - expectedBytes;
            long sleepMilliseconds = (long)((double)excessBytes / _bytesPerSecond * 1000);
            
            if (sleepMilliseconds > 0)
            {
                await Task.Delay((int)sleepMilliseconds, cancellationToken);
            }
        }
    }
} 