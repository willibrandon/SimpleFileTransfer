using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;

namespace SimpleFileTransfer;

public class FileTransferClient
{
    private readonly string _host;
    private readonly int _port;

    public FileTransferClient(string host, int port = Program.Port)
    {
        _host = host;
        _port = port;
    }

    public void SendFile(string filepath)
    {
        if (!File.Exists(filepath))
        {
            throw new FileNotFoundException($"File not found: {filepath}", filepath);
        }
        
        var fileInfo = new FileInfo(filepath);
        Console.WriteLine($"Sending {fileInfo.Name} ({fileInfo.Length:N0} bytes) to {_host}");
        
        // Calculate hash before sending
        Console.Write("Calculating file hash... ");
        var hash = Program.CalculateHash(filepath);
        Console.WriteLine("done");
        
        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();
            using var writer = new BinaryWriter(stream);
            
            // Send filename
            writer.Write(Path.GetFileName(filepath));
            // Send filesize
            writer.Write(fileInfo.Length);
            // Send hash
            writer.Write(hash);
            
            // Send file data
            using var fileStream = File.OpenRead(filepath);
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
                    Program.DisplayProgress(bytesRead, fileInfo.Length, bytesPerSecond);
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
    }

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
        
        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();
            using var writer = new BinaryWriter(stream);
            
            // Send marker indicating this is a directory
            writer.Write("DIR:");
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
                var hash = Program.CalculateHash(file);
                
                // Send relative path
                writer.Write(relativePath);
                // Send filesize
                writer.Write(fileInfo.Length);
                // Send hash
                writer.Write(hash);
                
                // Send file data
                using var fileStream = File.OpenRead(file);
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
                        Program.DisplayProgress(bytesRead, fileInfo.Length, bytesPerSecond);
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
    }
}
