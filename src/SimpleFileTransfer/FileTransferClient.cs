using System;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferClient"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
    /// <param name="useCompression">Whether to use compression for file transfers. Defaults to false.</param>
    public FileTransferClient(string host, int port = Program.Port, bool useCompression = false)
    {
        _host = host;
        _port = port;
        _useCompression = useCompression;
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
        Console.WriteLine($"Compression: {(_useCompression ? "Enabled" : "Disabled")}");
        
        // Calculate hash before sending
        Console.Write("Calculating file hash... ");
        var hash = FileOperations.CalculateHash(filepath);
        Console.WriteLine("done");
        
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
            // Send original filesize
            writer.Write(fileInfo.Length);
            // Send hash
            writer.Write(hash);
            
            // Send file data
            using var fileStream = File.OpenRead(filepath);
            
            if (_useCompression)
            {
                Console.WriteLine("Compressing data...");
                
                // Create a temporary file for compressed data
                var tempFile = Path.GetTempFileName();
                try
                {
                    // Compress the file
                    using (var compressedFileStream = File.Create(tempFile))
                    {
                        CompressionHelper.Compress(fileStream, compressedFileStream);
                    }
                    
                    // Get compressed size
                    var compressedInfo = new FileInfo(tempFile);
                    var compressedSize = compressedInfo.Length;
                    var ratio = CompressionHelper.GetCompressionRatio(fileInfo.Length, compressedSize);
                    Console.WriteLine($"Compressed: {compressedSize:N0} bytes ({ratio:F2}% reduction)");
                    
                    // Send compressed size
                    writer.Write(compressedSize);
                    
                    // Send compressed data
                    using var compressedDataStream = File.OpenRead(tempFile);
                    var buffer = new byte[8192];
                    var bytesRead = 0L;
                    var sw = Stopwatch.StartNew();
                    var lastUpdate = sw.ElapsedMilliseconds;
                    var lastBytes = 0L;
                    int read;
                    
                    while ((read = compressedDataStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, read);
                        bytesRead += read;
                        
                        var now = sw.ElapsedMilliseconds;
                        if (now - lastUpdate >= 100)
                        {
                            var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                            FileOperations.DisplayProgress(bytesRead, compressedSize, bytesPerSecond);
                            lastUpdate = now;
                            lastBytes = bytesRead;
                        }
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
            }
            else
            {
                // Send file data without compression
                var buffer = new byte[8192];
                var bytesRead = 0L;
                var sw = Stopwatch.StartNew();
                var lastUpdate = sw.ElapsedMilliseconds;
                var lastBytes = 0L;
                int read;
                
                while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    stream.Write(buffer, 0, read);
                    bytesRead += read;
                    
                    var now = sw.ElapsedMilliseconds;
                    if (now - lastUpdate >= 100)
                    {
                        var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                        FileOperations.DisplayProgress(bytesRead, fileInfo.Length, bytesPerSecond);
                        lastUpdate = now;
                        lastBytes = bytesRead;
                    }
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
        Console.WriteLine($"Compression: {(_useCompression ? "Enabled" : "Disabled")}");
        
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
                
                if (_useCompression)
                {
                    Console.WriteLine("Compressing data...");
                    
                    // Create a temporary file for compressed data
                    var tempFile = Path.GetTempFileName();
                    try
                    {
                        // Compress the file
                        using (var compressedFileStream = File.Create(tempFile))
                        {
                            CompressionHelper.Compress(fileStream, compressedFileStream);
                        }
                        
                        // Get compressed size
                        var compressedInfo = new FileInfo(tempFile);
                        var compressedSize = compressedInfo.Length;
                        var ratio = CompressionHelper.GetCompressionRatio(fileInfo.Length, compressedSize);
                        Console.WriteLine($"Compressed: {compressedSize:N0} bytes ({ratio:F2}% reduction)");
                        
                        // Send compressed size
                        writer.Write(compressedSize);
                        
                        // Send compressed data
                        using var compressedDataStream = File.OpenRead(tempFile);
                        var buffer = new byte[8192];
                        var bytesRead = 0L;
                        var sw = Stopwatch.StartNew();
                        var lastUpdate = sw.ElapsedMilliseconds;
                        var lastBytes = 0L;
                        int read;
                        
                        while ((read = compressedDataStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            stream.Write(buffer, 0, read);
                            bytesRead += read;
                            
                            var now = sw.ElapsedMilliseconds;
                            if (now - lastUpdate >= 100)
                            {
                                var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                                FileOperations.DisplayProgress(bytesRead, compressedSize, bytesPerSecond);
                                lastUpdate = now;
                                lastBytes = bytesRead;
                            }
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
                }
                else
                {
                    // Send file data without compression
                    var buffer = new byte[8192];
                    var bytesRead = 0L;
                    var sw = Stopwatch.StartNew();
                    var lastUpdate = sw.ElapsedMilliseconds;
                    var lastBytes = 0L;
                    int read;
                    
                    while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, read);
                        bytesRead += read;
                        
                        var now = sw.ElapsedMilliseconds;
                        if (now - lastUpdate >= 100)
                        {
                            var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                            FileOperations.DisplayProgress(bytesRead, fileInfo.Length, bytesPerSecond);
                            lastUpdate = now;
                            lastBytes = bytesRead;
                        }
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
    }
}
