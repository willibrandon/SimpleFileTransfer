using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SimpleFileTransfer;

/// <summary>
/// Handles server-side file transfer operations, including receiving files and directories.
/// </summary>
public class FileTransferServer
{
    private readonly int _port;
    private readonly string _downloadsDirectory;
    private readonly string? _password;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferServer"/> class.
    /// </summary>
    /// <param name="downloadsDirectory">The directory where received files will be saved.</param>
    /// <param name="port">The port number to listen on. Defaults to <see cref="Program.Port"/>.</param>
    /// <param name="password">The password to use for decryption. Required if receiving encrypted files.</param>
    public FileTransferServer(string downloadsDirectory, int port = Program.Port, string? password = null)
    {
        _port = port;
        _downloadsDirectory = downloadsDirectory;
        _password = password;
        Directory.CreateDirectory(_downloadsDirectory);
    }

    /// <summary>
    /// Starts the file transfer server and listens for incoming connections.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public void Start(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        
        var hostName = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(hostName)
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        
        Console.WriteLine($"Server started on port {_port}");
        Console.WriteLine("IP Addresses:");
        foreach (var ip in addresses)
        {
            var isTailscale = ip.ToString().StartsWith("100.");
            Console.WriteLine($"  {ip}{(isTailscale ? " (Tailscale)" : "")}");
        }
        Console.WriteLine("Ctrl+C to exit");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!listener.Pending())
                {
                    Thread.Sleep(100);  // Don't busy-wait
                    continue;
                }

                var client = listener.AcceptTcpClient();
                Console.WriteLine($"\nClient connected from {client.Client.RemoteEndPoint}");
                
                using var stream = client.GetStream();
                using var reader = new BinaryReader(stream);
                
                // Read first string to determine if it's a directory
                var firstString = reader.ReadString();
                
                if (firstString.StartsWith("DIR:"))
                {
                    ReceiveDirectory(reader, stream);
                }
                else
                {
                    // It's a single file transfer, firstString is the filename
                    ReceiveFile(reader, stream, firstString);
                }
                
                client.Close();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Receives a directory and all its contents from a client.
    /// </summary>
    /// <param name="reader">The binary reader for reading from the network stream.</param>
    /// <param name="stream">The network stream connected to the client.</param>
    private void ReceiveDirectory(BinaryReader reader, NetworkStream stream)
    {
        // Read compression flag
        var useCompression = reader.ReadBoolean();
        // Read encryption flag
        var useEncryption = reader.ReadBoolean();
        
        // Read compression algorithm if compression is enabled
        var compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip;
        if (useCompression)
        {
            compressionAlgorithm = (CompressionHelper.CompressionAlgorithm)reader.ReadInt32();
        }
        
        // Check if we have a password for decryption
        if (useEncryption && string.IsNullOrEmpty(_password))
        {
            Console.WriteLine("Error: Received encrypted data but no password was provided.");
            Console.WriteLine("Please restart the server with a password using the --password option.");
            return;
        }
        
        // Read base directory name
        var dirName = reader.ReadString();
        // Read number of files
        var fileCount = reader.ReadInt32();
        
        Console.WriteLine($"Receiving directory: {dirName}");
        Console.WriteLine($"Total files: {fileCount}");
        Console.WriteLine($"Compression: {(useCompression ? $"Enabled ({compressionAlgorithm})" : "Disabled")}");
        Console.WriteLine($"Encryption: {(useEncryption ? "Enabled" : "Disabled")}");
        
        // Create downloads directory
        var downloadsDir = Path.Combine(_downloadsDirectory, dirName);
        Directory.CreateDirectory(downloadsDir);
        
        for (var i = 0; i < fileCount; i++)
        {
            // Read relative path
            var relativePath = reader.ReadString();
            // Read original filesize
            var originalSize = reader.ReadInt64();
            // Read hash
            var sourceHash = reader.ReadString();
            // Read processed size
            var processedSize = reader.ReadInt64();
            
            Console.WriteLine($"\nReceiving file {i + 1}/{fileCount}: {relativePath}");
            
            // Create full save path
            var savePath = Path.Combine(downloadsDir, relativePath);
            Console.WriteLine($"Saving to: {savePath}");
            
            // Create directory if needed
            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            // Create a temporary file for processed data
            var tempFile = Path.GetTempFileName();
            try
            {
                // Receive processed data to temporary file
                using (var tempFileStream = File.Create(tempFile))
                {
                    var buffer = new byte[8192];
                    var bytesRead = 0L;
                    var sw = Stopwatch.StartNew();
                    var lastUpdate = sw.ElapsedMilliseconds;
                    var lastBytes = 0L;
                    
                    while (bytesRead < processedSize)
                    {
                        var chunkSize = (int)Math.Min(buffer.Length, processedSize - bytesRead);
                        var read = stream.Read(buffer, 0, chunkSize);
                        tempFileStream.Write(buffer, 0, read);
                        bytesRead += read;
                        
                        var now = sw.ElapsedMilliseconds;
                        if (now - lastUpdate >= 100)
                        {
                            var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                            FileOperations.DisplayProgress(bytesRead, processedSize, bytesPerSecond);
                            lastUpdate = now;
                            lastBytes = bytesRead;
                        }
                    }
                }
                
                Console.WriteLine();
                
                // Process the received data (decrypt and/or decompress)
                if (useEncryption || useCompression)
                {
                    var processedTempFile = Path.GetTempFileName();
                    try
                    {
                        var sourceStream = File.OpenRead(tempFile);
                        
                        // Decrypt if needed
                        if (useEncryption)
                        {
                            Console.WriteLine("Decrypting data...");
                            
                            var decryptedTempFile = Path.GetTempFileName();
                            try
                            {
                                using (var decryptedStream = File.Create(decryptedTempFile))
                                {
                                    bool decryptionSuccess = EncryptionHelper.Decrypt(sourceStream, decryptedStream, _password!);
                                    
                                    if (!decryptionSuccess)
                                    {
                                        Console.WriteLine("Warning: Decryption failed. File may be corrupted.");
                                        // Continue with the encrypted data, hash verification will likely fail
                                    }
                                }
                                
                                sourceStream.Dispose();
                                sourceStream = File.OpenRead(decryptedTempFile);
                            }
                            finally
                            {
                                // Clean up temporary file
                                if (File.Exists(decryptedTempFile) && decryptedTempFile != processedTempFile)
                                {
                                    try
                                    {
                                        File.Delete(decryptedTempFile);
                                    }
                                    catch (IOException)
                                    {
                                        // Ignore file access errors during cleanup
                                    }
                                }
                            }
                        }
                        
                        // Decompress if needed
                        if (useCompression)
                        {
                            Console.WriteLine($"Decompressing data using {compressionAlgorithm}...");
                            
                            using (var decompressedStream = File.Create(processedTempFile))
                            {
                                CompressionHelper.Decompress(sourceStream, decompressedStream, compressionAlgorithm);
                            }
                        }
                        else
                        {
                            // Just copy the data if no decompression needed
                            using (var destStream = File.Create(processedTempFile))
                            {
                                sourceStream.CopyTo(destStream);
                            }
                        }
                        
                        // Close the source stream
                        sourceStream.Dispose();
                        
                        // Copy the processed file to the final location
                        File.Copy(processedTempFile, savePath, true);
                    }
                    finally
                    {
                        // Clean up temporary file
                        if (File.Exists(processedTempFile))
                        {
                            File.Delete(processedTempFile);
                        }
                    }
                }
                else
                {
                    // No processing needed, just copy the file
                    File.Copy(tempFile, savePath, true);
                }
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            
            // Verify hash
            Console.Write("Verifying file hash... ");
            var calculatedHash = FileOperations.CalculateHash(savePath);
            if (sourceHash == calculatedHash)
            {
                Console.WriteLine("File received and verified: " + savePath);
            }
            else
            {
                Console.WriteLine("Warning: File hash mismatch!");
                Console.WriteLine("Expected: " + sourceHash);
                Console.WriteLine("Calculated: " + calculatedHash);
            }
        }
        
        Console.WriteLine("\nDirectory received successfully");
    }

    /// <summary>
    /// Receives a single file from a client.
    /// </summary>
    /// <param name="reader">The binary reader for reading from the network stream.</param>
    /// <param name="stream">The network stream connected to the client.</param>
    /// <param name="filename">The name of the file being received.</param>
    private void ReceiveFile(BinaryReader reader, NetworkStream stream, string filename)
    {
        // Read compression flag
        var useCompression = reader.ReadBoolean();
        // Read encryption flag
        var useEncryption = reader.ReadBoolean();
        // Read original filesize
        var originalSize = reader.ReadInt64();
        // Read hash
        var sourceHash = reader.ReadString();
        
        // Check if we have a password for decryption
        if (useEncryption && string.IsNullOrEmpty(_password))
        {
            Console.WriteLine("Error: Received encrypted data but no password was provided.");
            Console.WriteLine("Please restart the server with a password using the --password option.");
            return;
        }
        
        // Read compression algorithm if compression is enabled
        var compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip;
        if (useCompression)
        {
            compressionAlgorithm = (CompressionHelper.CompressionAlgorithm)reader.ReadInt32();
        }
        
        // Create downloads directory
        Directory.CreateDirectory(_downloadsDirectory);
        
        // Create full save path
        var savePath = Path.Combine(_downloadsDirectory, filename);
        Console.WriteLine($"Receiving file: {filename} ({originalSize:N0} bytes)");
        Console.WriteLine($"Compression: {(useCompression ? $"Enabled ({compressionAlgorithm})" : "Disabled")}");
        Console.WriteLine($"Encryption: {(useEncryption ? "Enabled" : "Disabled")}");
        Console.WriteLine($"Saving to: {savePath}");
        
        // Create directory if needed
        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        // Read processed size
        var processedSize = reader.ReadInt64();
        
        // Create a temporary file for processed data
        var tempFile = Path.GetTempFileName();
        try
        {
            // Receive processed data to temporary file
            using (var tempFileStream = File.Create(tempFile))
            {
                var buffer = new byte[8192];
                var bytesRead = 0L;
                var sw = Stopwatch.StartNew();
                var lastUpdate = sw.ElapsedMilliseconds;
                var lastBytes = 0L;
                
                while (bytesRead < processedSize)
                {
                    var chunkSize = (int)Math.Min(buffer.Length, processedSize - bytesRead);
                    var read = stream.Read(buffer, 0, chunkSize);
                    tempFileStream.Write(buffer, 0, read);
                    bytesRead += read;
                    
                    var now = sw.ElapsedMilliseconds;
                    if (now - lastUpdate >= 100)
                    {
                        var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                        FileOperations.DisplayProgress(bytesRead, processedSize, bytesPerSecond);
                        lastUpdate = now;
                        lastBytes = bytesRead;
                    }
                }
            }
            
            Console.WriteLine();
            
            // Process the received data (decrypt and/or decompress)
            if (useEncryption || useCompression)
            {
                var processedTempFile = Path.GetTempFileName();
                try
                {
                    var sourceStream = File.OpenRead(tempFile);
                    
                    // Decrypt if needed
                    if (useEncryption)
                    {
                        Console.WriteLine("Decrypting data...");
                        
                        var decryptedTempFile = Path.GetTempFileName();
                        try
                        {
                            using (var decryptedStream = File.Create(decryptedTempFile))
                            {
                                bool decryptionSuccess = EncryptionHelper.Decrypt(sourceStream, decryptedStream, _password!);
                                
                                if (!decryptionSuccess)
                                {
                                    Console.WriteLine("Warning: Decryption failed. File may be corrupted.");
                                    // Continue with the encrypted data, hash verification will likely fail
                                }
                            }
                            
                            sourceStream.Dispose();
                            sourceStream = File.OpenRead(decryptedTempFile);
                        }
                        finally
                        {
                            // Clean up temporary file
                            if (File.Exists(decryptedTempFile) && decryptedTempFile != processedTempFile)
                            {
                                try
                                {
                                    File.Delete(decryptedTempFile);
                                }
                                catch (IOException)
                                {
                                    // Ignore file access errors during cleanup
                                }
                            }
                        }
                    }
                    
                    // Decompress if needed
                    if (useCompression)
                    {
                        Console.WriteLine($"Decompressing data using {compressionAlgorithm}...");
                        
                        using (var decompressedStream = File.Create(processedTempFile))
                        {
                            CompressionHelper.Decompress(sourceStream, decompressedStream, compressionAlgorithm);
                        }
                    }
                    else
                    {
                        // Just copy the data if no decompression needed
                        using (var destStream = File.Create(processedTempFile))
                        {
                            sourceStream.CopyTo(destStream);
                        }
                    }
                    
                    // Close the source stream
                    sourceStream.Dispose();
                    
                    // Copy the processed file to the final location
                    File.Copy(processedTempFile, savePath, true);
                }
                finally
                {
                    // Clean up temporary file
                    if (File.Exists(processedTempFile))
                    {
                        File.Delete(processedTempFile);
                    }
                }
            }
            else
            {
                // No processing needed, just copy the file
                File.Copy(tempFile, savePath, true);
            }
        }
        finally
        {
            // Clean up temporary file
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
        
        // Verify hash
        Console.Write("Verifying file hash... ");
        var calculatedHash = FileOperations.CalculateHash(savePath);
        if (sourceHash == calculatedHash)
        {
            Console.WriteLine("File received and verified: " + savePath);
        }
        else
        {
            Console.WriteLine("Warning: File hash mismatch!");
            Console.WriteLine("Expected: " + sourceHash);
            Console.WriteLine("Calculated: " + calculatedHash);
        }
    }
}
