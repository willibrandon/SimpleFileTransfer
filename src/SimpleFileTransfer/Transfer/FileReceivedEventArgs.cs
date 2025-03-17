using System;

namespace SimpleFileTransfer.Transfer;

/// <summary>
/// Provides data for the <see cref="FileTransferServer.FileReceived"/> event.
/// </summary>
public class FileReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="filePath">The path to the received file.</param>
    /// <param name="originalSize">The original size of the file in bytes.</param>
    /// <param name="senderIp">The IP address of the sender.</param>
    public FileReceivedEventArgs(string filePath, long originalSize, string? senderIp)
    {
        FilePath = filePath;
        OriginalSize = originalSize;
        SenderIp = senderIp;
    }

    /// <summary>
    /// Gets the path to the received file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the original size of the file in bytes.
    /// </summary>
    public long OriginalSize { get; }

    /// <summary>
    /// Gets the IP address of the sender.
    /// </summary>
    public string? SenderIp { get; }
} 