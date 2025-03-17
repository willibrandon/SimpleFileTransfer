using System;
using System.IO;
using SimpleFileTransfer.Helpers;

namespace SimpleFileTransfer.Core;

/// <summary>
/// Decorator that adds encryption functionality to file transfers.
/// </summary>
/// <remarks>
/// This class implements the decorator pattern to add encryption behavior to any <see cref="FileTransfer"/> instance.
/// It encrypts the source file before transferring it to the destination.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="EncryptedFileTransfer"/> class.
/// </remarks>
/// <param name="decoratedTransfer">The file transfer instance to decorate with encryption functionality.</param>
/// <param name="password">The password to use for encryption.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="decoratedTransfer"/> or <paramref name="password"/> is null.</exception>
public class EncryptedFileTransfer(FileTransfer decoratedTransfer, string password) : FileTransfer
{
    private readonly FileTransfer _decoratedTransfer = decoratedTransfer
        ?? throw new ArgumentNullException(nameof(decoratedTransfer));

    private readonly string _password = password
        ?? throw new ArgumentNullException(nameof(password));

    /// <summary>
    /// Transfers a file from source to destination with encryption.
    /// </summary>
    /// <param name="sourcePath">The full path to the source file.</param>
    /// <param name="destinationPath">The full path where the file should be copied to.</param>
    /// <exception cref="ArgumentException">Thrown when source or destination path is null or empty.</exception>
    /// <remarks>
    /// This method encrypts the source file to a temporary location and then
    /// uses the decorated transfer to move the encrypted file to the destination.
    /// </remarks>
    public override void Transfer(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (string.IsNullOrEmpty(destinationPath))
            throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));

        // Create a temporary file for the encrypted data
        var tempFile = Path.GetTempFileName();

        try
        {
            // Encrypt the source file to the temp file
            using (var sourceStream = File.OpenRead(sourcePath))
            using (var tempStream = File.Create(tempFile))
            {
                // Use the EncryptionHelper to encrypt the file with the password
                EncryptionHelper.Encrypt(sourceStream, tempStream, _password);
            }

            // Transfer the encrypted file to the destination
            _decoratedTransfer.Transfer(tempFile, destinationPath);
        }
        finally
        {
            // Clean up the temporary file
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
