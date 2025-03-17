using SimpleFileTransfer.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Services;

/// <summary>
/// Service that handles file transfer operations with optional compression and encryption.
/// </summary>
/// <remarks>
/// This class implements the <see cref="IFileTransferService"/> interface and provides
/// functionality for transferring files with various options. It uses the decorator pattern
/// to add compression and encryption capabilities to the basic file transfer operation.
/// </remarks>
public class FileTransferService : IFileTransferService
{
    /// <summary>
    /// Transfers a file asynchronously according to the specified options.
    /// </summary>
    /// <param name="options">The options specifying the source, destination, and transfer settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when source or destination path is null or empty, or when encryption is enabled but no password is provided.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    public async Task TransferFileAsync(FileTransferOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.SourcePath))
        {
            throw new ArgumentException(message: "Source path cannot be empty", paramName: nameof(options));
        }

        if (string.IsNullOrEmpty(options.DestinationPath))
        {
            throw new ArgumentException(message: "Destination path cannot be empty", paramName: nameof(options));
        }

        if (options.Encrypt && string.IsNullOrEmpty(options.Password))
        {
            throw new ArgumentException(message: "Password is required for encryption", paramName: nameof(options));
        }

        if (!File.Exists(options.SourcePath))
        {
            throw new FileNotFoundException("Source file not found", options.SourcePath);
        }

        // Create a new FileTransfer instance
        FileTransfer transfer = new BasicFileTransfer();

        // Apply speed limit if specified
        if (options.SpeedLimit.HasValue && options.SpeedLimit.Value > 0)
        {
            transfer = new ThrottledFileTransfer(transfer, options.SpeedLimit.Value);
        }

        if (options.Compress)
        {
            transfer = new CompressedFileTransfer(transfer);
        }

        if (options.Encrypt && options.Password != null)
        {
            transfer = new EncryptedFileTransfer(transfer, options.Password);
        }

        await Task.Run(() => transfer.Transfer(options.SourcePath, options.DestinationPath));
    }
}
