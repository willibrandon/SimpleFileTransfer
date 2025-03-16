using System;
using System.Collections.Generic;
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
    private bool _isRunning;
    private TcpListener? _listener;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferServer"/> class.
    /// </summary>
    /// <param name="downloadsDirectory">The directory where received files will be saved.</param>
    /// <param name="port">The port number to listen on. Defaults to <see cref="Program.Port"/>.</param>
    /// <param name="password">The password to use for decryption. Required if receiving encrypted files.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public FileTransferServer(
        string downloadsDirectory, 
        int port = Program.Port, 
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        _port = port;
        _downloadsDirectory = downloadsDirectory;
        _password = password;
        _cancellationToken = cancellationToken;
        Directory.CreateDirectory(_downloadsDirectory);
    }

    /// <summary>
    /// Starts the file transfer server and listens for incoming connections.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        
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
        
        Thread.Sleep(100); // Give the console a moment to display the message
        
        // Start accepting clients in a background thread
        ThreadPool.QueueUserWorkItem(_ => AcceptClients());
    }

    /// <summary>
    /// Stops the file transfer server.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        Console.WriteLine("Server stopped");
    }

    private void AcceptClients()
    {
        try
        {
            while (_isRunning && !_cancellationToken.IsCancellationRequested)
            {
                if (!_listener!.Pending())
                {
                    Thread.Sleep(100);  // Don't busy-wait
                    continue;
                }

                var client = _listener.AcceptTcpClient();
                Console.WriteLine($"\nClient connected from {client.Client.RemoteEndPoint}");
                
                // Handle client in a separate thread
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted || ex.SocketErrorCode == SocketError.OperationAborted)
        {
            // Server was stopped, this is expected
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error accepting client: {ex.Message}");
        }
        finally
        {
            _listener?.Stop();
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                using var stream = client.GetStream();
                using var reader = new BinaryReader(stream);
                
                // Read first string to determine if it's a directory or multiple files
                var firstString = reader.ReadString();
                
                if (firstString.StartsWith("DIR:"))
                {
                    ReceiveDirectory(reader, stream);
                }
                else if (firstString.StartsWith("MULTI:"))
                {
                    ReceiveMultipleFiles(reader, stream);
                }
                else
                {
                    // It's a single file transfer, firstString is the filename
                    ReceiveFile(reader, stream, firstString);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
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
        // Read resume flag
        var resumeEnabled = reader.ReadBoolean();
        
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
        Console.WriteLine($"Resume capability: {(resumeEnabled ? "Enabled" : "Disabled")}");
        
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
            // Read resume position
            var resumePosition = reader.ReadInt64();
            // Read processed size
            var processedSize = reader.ReadInt64();
            // Read processed resume position
            var processedResumePosition = reader.ReadInt64();
            
            Console.WriteLine($"\nReceiving file {i + 1}/{fileCount}: {relativePath}");
            
            if (resumeEnabled && resumePosition > 0)
            {
                Console.WriteLine($"Resuming from position {resumePosition:N0} bytes ({(double)resumePosition * 100 / originalSize:F2}%)");
                Console.WriteLine($"Processed resume position: {processedResumePosition:N0} bytes ({(double)processedResumePosition * 100 / processedSize:F2}%)");
            }
            
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
                // If resuming and the file exists, copy the existing file to the temp file
                if (resumeEnabled && resumePosition > 0 && File.Exists(savePath))
                {
                    using var existingFile = File.OpenRead(savePath);
                    using var tempFileStream = File.Create(tempFile);
                    existingFile.CopyTo(tempFileStream);
                }
                
                // Receive processed data to temporary file
                using (var tempFileStream = new FileStream(tempFile, FileMode.Append))
                {
                    var buffer = new byte[8192];
                    var bytesRead = processedResumePosition;
                    var sw = Stopwatch.StartNew();
                    var lastUpdate = sw.ElapsedMilliseconds;
                    var lastBytes = bytesRead;
                    
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
                            
                            try
                            {
                                using var decompressedStream = File.Create(processedTempFile);
                                CompressionHelper.Decompress(sourceStream, decompressedStream, compressionAlgorithm);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Decompression failed: {ex.Message}");
                                Console.WriteLine("The file may be corrupted.");
                                
                                // If decompression fails, just copy the source data
                                sourceStream.Position = 0;
                                using var destStream = File.Create(processedTempFile);
                                sourceStream.CopyTo(destStream);
                            }
                        }
                        else
                        {
                            // Just copy the data if no decompression needed
                            using var destStream = File.Create(processedTempFile);
                            sourceStream.CopyTo(destStream);
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
                            try
                            {
                                File.Delete(processedTempFile);
                            }
                            catch (IOException)
                            {
                                // Ignore file access errors during cleanup
                            }
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
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (IOException)
                    {
                        // Ignore file access errors during cleanup
                    }
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
        // Read resume flag and position
        var resumeEnabled = reader.ReadBoolean();
        var resumePosition = reader.ReadInt64();
        
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
        Console.WriteLine($"Resume capability: {(resumeEnabled ? "Enabled" : "Disabled")}");
        Console.WriteLine($"Saving to: {savePath}");
        
        if (resumeEnabled && resumePosition > 0)
        {
            Console.WriteLine($"Resuming from position {resumePosition:N0} bytes ({(double)resumePosition * 100 / originalSize:F2}%)");
        }
        
        // Create directory if needed
        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        // Read processed size
        var processedSize = reader.ReadInt64();
        // Read processed resume position
        var processedResumePosition = reader.ReadInt64();
        
        if (resumeEnabled && processedResumePosition > 0)
        {
            Console.WriteLine($"Processed resume position: {processedResumePosition:N0} bytes ({(double)processedResumePosition * 100 / processedSize:F2}%)");
        }
        
        // Create a temporary file for processed data
        var tempFile = Path.GetTempFileName();
        try
        {
            // If resuming and the file exists, copy the existing file to the temp file
            if (resumeEnabled && resumePosition > 0 && File.Exists(savePath))
            {
                using var existingFile = File.OpenRead(savePath);
                using var tempFileStream = File.Create(tempFile);
                existingFile.CopyTo(tempFileStream);
            }
            
            // Receive processed data to temporary file
            using (var tempFileStream = new FileStream(tempFile, FileMode.Append))
            {
                var buffer = new byte[8192];
                var bytesRead = processedResumePosition;
                var sw = Stopwatch.StartNew();
                var lastUpdate = sw.ElapsedMilliseconds;
                var lastBytes = bytesRead;
                
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
                        
                        try
                        {
                            using var decompressedStream = File.Create(processedTempFile);
                            CompressionHelper.Decompress(sourceStream, decompressedStream, compressionAlgorithm);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Decompression failed: {ex.Message}");
                            Console.WriteLine("The file may be corrupted.");
                            
                            // If decompression fails, just copy the source data
                            sourceStream.Position = 0;
                            using var destStream = File.Create(processedTempFile);
                            sourceStream.CopyTo(destStream);
                        }
                    }
                    else
                    {
                        // Just copy the data if no decompression needed
                        using var destStream = File.Create(processedTempFile);
                        sourceStream.CopyTo(destStream);
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
                        try
                        {
                            File.Delete(processedTempFile);
                        }
                        catch (IOException)
                        {
                            // Ignore file access errors during cleanup
                        }
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
                try
                {
                    File.Delete(tempFile);
                }
                catch (IOException)
                {
                    // Ignore file access errors during cleanup
                }
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

    /// <summary>
    /// Receives multiple files from a client.
    /// </summary>
    /// <param name="reader">The binary reader for reading from the network stream.</param>
    /// <param name="stream">The network stream connected to the client.</param>
    private void ReceiveMultipleFiles(BinaryReader reader, NetworkStream stream)
    {
        // Read compression flag
        var useCompression = reader.ReadBoolean();
        // Read encryption flag
        var useEncryption = reader.ReadBoolean();
        // Read resume flag
        var resumeEnabled = reader.ReadBoolean();
        
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
        
        // Read number of files
        var fileCount = reader.ReadInt32();
        
        Console.WriteLine($"Receiving multiple files: {fileCount} files");
        Console.WriteLine($"Compression: {(useCompression ? $"Enabled ({compressionAlgorithm})" : "Disabled")}");
        Console.WriteLine($"Encryption: {(useEncryption ? "Enabled" : "Disabled")}");
        Console.WriteLine($"Resume capability: {(resumeEnabled ? "Enabled" : "Disabled")}");
        
        // Create downloads directory
        Directory.CreateDirectory(_downloadsDirectory);
        
        for (var i = 0; i < fileCount; i++)
        {
            // Read filename
            var filename = reader.ReadString();
            // Read original filesize
            var originalSize = reader.ReadInt64();
            // Read hash
            var sourceHash = reader.ReadString();
            // Read resume position
            var resumePosition = reader.ReadInt64();
            // Read processed size
            var processedSize = reader.ReadInt64();
            // Read processed resume position
            var processedResumePosition = reader.ReadInt64();
            
            Console.WriteLine($"\nReceiving file {i + 1}/{fileCount}: {filename}");
            
            if (resumeEnabled && resumePosition > 0)
            {
                Console.WriteLine($"Resuming from position {resumePosition:N0} bytes ({(double)resumePosition * 100 / originalSize:F2}%)");
                Console.WriteLine($"Processed resume position: {processedResumePosition:N0} bytes ({(double)processedResumePosition * 100 / processedSize:F2}%)");
            }
            
            // Create full save path
            var savePath = Path.Combine(_downloadsDirectory, filename);
            Console.WriteLine($"Saving to: {savePath}");
            
            // Create a temporary file for processed data
            var tempFile = Path.GetTempFileName();
            try
            {
                // If resuming and the file exists, copy the existing file to the temp file
                if (resumeEnabled && resumePosition > 0 && File.Exists(savePath))
                {
                    using var existingFile = File.OpenRead(savePath);
                    using var tempFileStream = File.Create(tempFile);
                    existingFile.CopyTo(tempFileStream);
                }
                
                // Receive processed data to temporary file
                using (var tempFileStream = new FileStream(tempFile, FileMode.Append))
                {
                    var buffer = new byte[8192];
                    var bytesRead = processedResumePosition;
                    var sw = Stopwatch.StartNew();
                    var lastUpdate = sw.ElapsedMilliseconds;
                    var lastBytes = bytesRead;
                    
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
                            
                            try
                            {
                                using var decompressedStream = File.Create(processedTempFile);
                                CompressionHelper.Decompress(sourceStream, decompressedStream, compressionAlgorithm);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Decompression failed: {ex.Message}");
                                Console.WriteLine("The file may be corrupted.");
                                
                                // If decompression fails, just copy the source data
                                sourceStream.Position = 0;
                                using var destStream = File.Create(processedTempFile);
                                sourceStream.CopyTo(destStream);
                            }
                        }
                        else
                        {
                            // Just copy the data if no decompression needed
                            using var destStream = File.Create(processedTempFile);
                            sourceStream.CopyTo(destStream);
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
                            try
                            {
                                File.Delete(processedTempFile);
                            }
                            catch (IOException)
                            {
                                // Ignore file access errors during cleanup
                            }
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
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (IOException)
                    {
                        // Ignore file access errors during cleanup
                    }
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
        
        Console.WriteLine("\nAll files received successfully");
    }
}
