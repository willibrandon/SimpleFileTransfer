using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;

namespace SimpleFileTransfer;

/// <summary>
/// Handles client-side file transfer operations, including sending files and directories.
/// </summary>
public class FileTransferClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly bool _useCompression;
    private readonly CompressionHelper.CompressionAlgorithm _compressionAlgorithm;
    private readonly bool _useEncryption;
    private readonly string? _password;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferClient"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
    /// <param name="useCompression">Whether to use compression for file transfers. Defaults to false.</param>
    /// <param name="compressionAlgorithm">The compression algorithm to use. Defaults to GZip.</param>
    /// <param name="useEncryption">Whether to use encryption for file transfers. Defaults to false.</param>
    /// <param name="password">The password to use for encryption. Required if useEncryption is true.</param>
    public FileTransferClient(
        string host, 
        int port = Program.Port, 
        bool useCompression = false, 
        CompressionHelper.CompressionAlgorithm compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
        bool useEncryption = false,
        string? password = null)
    {
        _host = host;
        _port = port;
        _useCompression = useCompression;
        _compressionAlgorithm = compressionAlgorithm;
        _useEncryption = useEncryption;
        _password = password;
        
        if (_useEncryption && string.IsNullOrEmpty(_password))
        {
            throw new ArgumentException("Password is required when encryption is enabled.", nameof(password));
        }
    }

    /// <summary>
    /// Sends a single file to the server.
    /// </summary>
    /// <param name="filepath">The path to the file to send.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public void SendFile(string filepath)
    {
        if (!File.Exists(filepath))
        {
            throw new FileNotFoundException($"File not found: {filepath}", filepath);
        }
        
        var fileInfo = new FileInfo(filepath);
        Console.WriteLine($"Sending {fileInfo.Name} ({fileInfo.Length:N0} bytes) to {_host}");
        Console.WriteLine($"Compression: {(_useCompression ? $"Enabled ({_compressionAlgorithm})" : "Disabled")}");
        Console.WriteLine($"Encryption: {(_useEncryption ? "Enabled" : "Disabled")}");
        
        // Calculate hash before sending
        Console.Write("Calculating file hash... ");
        var hash = FileOperations.CalculateHash(filepath);
        Console.WriteLine("done");
        
        // Keep track of temporary files to clean up at the end
        var tempFiles = new List<string>();
        
        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();
            using var writer = new BinaryWriter(stream);
            
            // Send filename
            writer.Write(Path.GetFileName(filepath));
            // Send compression flag
            writer.Write(_useCompression);
            // Send encryption flag
            writer.Write(_useEncryption);
            // Send original filesize
            writer.Write(fileInfo.Length);
            // Send hash
            writer.Write(hash);
            
            // Send file data
            using var fileStream = File.OpenRead(filepath);
            
            // Create a temporary file for processed data
            var tempFile = Path.GetTempFileName();
            tempFiles.Add(tempFile);
            
            // Process the file (compression and/or encryption)
            using (var processedFileStream = File.Create(tempFile))
            {
                var sourceStream = fileStream;
                
                // Apply compression if enabled
                if (_useCompression)
                {
                    // Send compression algorithm
                    writer.Write((int)_compressionAlgorithm);
                    
                    Console.WriteLine($"Compressing data using {_compressionAlgorithm}...");
                    
                    var compressedTempFile = Path.GetTempFileName();
                    tempFiles.Add(compressedTempFile);
                    
                    try
                    {
                        using (var compressedFileStream = File.Create(compressedTempFile))
                        {
                            CompressionHelper.Compress(sourceStream, compressedFileStream, _compressionAlgorithm);
                        }
                        
                        // Close the original source stream before opening the new one
                        if (sourceStream != fileStream)
                        {
                            sourceStream.Dispose();
                        }
                        
                        sourceStream = File.OpenRead(compressedTempFile);
                    }
                    finally
                    {
                        // We'll clean up this file later, after sourceStream is closed
                        // Don't delete it here as it might still be in use by sourceStream
                    }
                }
                
                // Apply encryption if enabled
                if (_useEncryption)
                {
                    Console.WriteLine("Encrypting data...");
                    EncryptionHelper.Encrypt(sourceStream, processedFileStream, _password!);
                    
                    // Close the source stream if it's not the original file stream
                    if (sourceStream != fileStream)
                    {
                        sourceStream.Dispose();
                    }
                }
                else
                {
                    // Just copy the data if no encryption
                    sourceStream.CopyTo(processedFileStream);
                    
                    // Close the source stream if it's not the original file stream
                    if (sourceStream != fileStream)
                    {
                        sourceStream.Dispose();
                    }
                }
            }
            
            // Get processed size
            var processedInfo = new FileInfo(tempFile);
            var processedSize = processedInfo.Length;
            
            if (_useCompression)
            {
                var ratio = CompressionHelper.GetCompressionRatio(fileInfo.Length, processedSize);
                Console.WriteLine($"Processed size: {processedSize:N0} bytes ({ratio:F2}% reduction)");
            }
            else
            {
                Console.WriteLine($"Processed size: {processedSize:N0} bytes");
            }
            
            // Send processed size
            writer.Write(processedSize);
            
            // Send processed data
            using var processedDataStream = File.OpenRead(tempFile);
            var buffer = new byte[8192];
            var bytesRead = 0L;
            var sw = Stopwatch.StartNew();
            var lastUpdate = sw.ElapsedMilliseconds;
            var lastBytes = 0L;
            int read;
            
            while ((read = processedDataStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                stream.Write(buffer, 0, read);
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
            
            Console.WriteLine();
            Console.WriteLine("File sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
        finally
        {
            // Clean up all temporary files
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch (IOException)
                {
                    // Ignore file access errors during cleanup
                    Console.WriteLine($"Note: Could not delete temporary file {tempFile}. It will be cleaned up later.");
                }
            }
        }
    }

    /// <summary>
    /// Sends a directory and all its contents to the server.
    /// </summary>
    /// <param name="dirPath">The path to the directory to send.</param>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory does not exist.</exception>
    public void SendDirectory(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {dirPath}");
        }
        
        var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
        var dirInfo = new DirectoryInfo(dirPath);
        var totalSize = files.Sum(f => new FileInfo(f).Length);
        
        Console.WriteLine($"Preparing to send directory: {dirPath}");
        Console.WriteLine($"Total files: {files.Length}");
        Console.WriteLine($"Total size: {totalSize:N0} bytes");
        Console.WriteLine($"Compression: {(_useCompression ? $"Enabled ({_compressionAlgorithm})" : "Disabled")}");
        Console.WriteLine($"Encryption: {(_useEncryption ? "Enabled" : "Disabled")}");
        
        // Keep track of temporary files to clean up at the end
        var tempFiles = new List<string>();
        
        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();
            using var writer = new BinaryWriter(stream);
            
            // Send marker indicating this is a directory
            writer.Write("DIR:");
            // Send compression flag
            writer.Write(_useCompression);
            // Send encryption flag
            writer.Write(_useEncryption);
            
            if (_useCompression)
            {
                // Send compression algorithm
                writer.Write((int)_compressionAlgorithm);
            }
            
            // Send base directory name
            writer.Write(dirInfo.Name);
            // Send number of files
            writer.Write(files.Length);
            
            foreach (var (file, index) in files.Select((f, i) => (f, i)))
            {
                var fileInfo = new FileInfo(file);
                var relativePath = Path.GetRelativePath(dirPath, file);
                
                Console.WriteLine($"\nSending file {index + 1}/{files.Length}: {relativePath}");
                
                // Calculate hash
                var hash = FileOperations.CalculateHash(file);
                
                // Send relative path
                writer.Write(relativePath);
                // Send original filesize
                writer.Write(fileInfo.Length);
                // Send hash
                writer.Write(hash);
                
                // Send file data
                using var fileStream = File.OpenRead(file);
                
                // Create a temporary file for processed data
                var tempFile = Path.GetTempFileName();
                tempFiles.Add(tempFile);
                
                // Process the file (compression and/or encryption)
                using (var processedFileStream = File.Create(tempFile))
                {
                    var sourceStream = fileStream;
                    
                    // Apply compression if enabled
                    if (_useCompression)
                    {
                        Console.WriteLine($"Compressing data using {_compressionAlgorithm}...");
                        
                        var compressedTempFile = Path.GetTempFileName();
                        tempFiles.Add(compressedTempFile);
                        
                        try
                        {
                            using (var compressedFileStream = File.Create(compressedTempFile))
                            {
                                CompressionHelper.Compress(sourceStream, compressedFileStream, _compressionAlgorithm);
                            }
                            
                            // Close the original source stream before opening the new one
                            if (sourceStream != fileStream)
                            {
                                sourceStream.Dispose();
                            }
                            
                            sourceStream = File.OpenRead(compressedTempFile);
                        }
                        finally
                        {
                            // We'll clean up this file later, after sourceStream is closed
                            // Don't delete it here as it might still be in use by sourceStream
                        }
                    }
                    
                    // Apply encryption if enabled
                    if (_useEncryption)
                    {
                        Console.WriteLine("Encrypting data...");
                        EncryptionHelper.Encrypt(sourceStream, processedFileStream, _password!);
                        
                        // Close the source stream if it's not the original file stream
                        if (sourceStream != fileStream)
                        {
                            sourceStream.Dispose();
                        }
                    }
                    else
                    {
                        // Just copy the data if no encryption
                        sourceStream.CopyTo(processedFileStream);
                        
                        // Close the source stream if it's not the original file stream
                        if (sourceStream != fileStream)
                        {
                            sourceStream.Dispose();
                        }
                    }
                }
                
                // Get processed size
                var processedInfo = new FileInfo(tempFile);
                var processedSize = processedInfo.Length;
                
                if (_useCompression)
                {
                    var ratio = CompressionHelper.GetCompressionRatio(fileInfo.Length, processedSize);
                    Console.WriteLine($"Processed size: {processedSize:N0} bytes ({ratio:F2}% reduction)");
                }
                else
                {
                    Console.WriteLine($"Processed size: {processedSize:N0} bytes");
                }
                
                // Send processed size
                writer.Write(processedSize);
                
                // Send processed data
                using var processedDataStream = File.OpenRead(tempFile);
                var buffer = new byte[8192];
                var bytesRead = 0L;
                var sw = Stopwatch.StartNew();
                var lastUpdate = sw.ElapsedMilliseconds;
                var lastBytes = 0L;
                int read;
                
                while ((read = processedDataStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    stream.Write(buffer, 0, read);
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
                
                Console.WriteLine();
            }
            
            Console.WriteLine("\nDirectory sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
        finally
        {
            // Clean up all temporary files
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch (IOException)
                {
                    // Ignore file access errors during cleanup
                    Console.WriteLine($"Note: Could not delete temporary file {tempFile}. It will be cleaned up later.");
                }
            }
        }
    }
}
