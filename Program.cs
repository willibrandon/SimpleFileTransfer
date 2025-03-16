using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;

namespace SimpleFileTransfer;

class Program
{
    const int Port = 9876;
    
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  receive - Start receiving files");
            Console.WriteLine("  send <host> <filepath> - Send a file");
            return;
        }

        if (args[0] == "receive")
        {
            RunServer();
        }
        else if (args[0] == "send" && args.Length == 3)
        {
            SendFile(args[1], args[2]);
        }
    }

    static string CalculateHash(string filepath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filepath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    static void DisplayProgress(long current, long total, long bytesPerSecond)
    {
        var percentage = current * 100 / total;
        var mbps = bytesPerSecond / 1024.0 / 1024.0;
        Console.Write($"\rProgress: {current:N0}/{total:N0} bytes ({percentage}%) - {mbps:F2} MB/s");
    }

    static void RunServer()
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        
        var hostName = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(hostName)
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        
        Console.WriteLine($"Server started on port {Port}");
        Console.WriteLine("IP Addresses:");
        foreach (var ip in addresses)
        {
            var isTailscale = ip.ToString().StartsWith("100.");
            Console.WriteLine($"  {ip}{(isTailscale ? " (Tailscale)" : "")}");
        }
        Console.WriteLine("Ctrl+C to exit");
        
        while (true)
        {
            var client = listener.AcceptTcpClient();
            Console.WriteLine($"Client connected from {client.Client.RemoteEndPoint}");
            
            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream);
            
            // Read filename
            var filename = reader.ReadString();
            // Read filesize
            var filesize = reader.ReadInt64();
            // Read hash
            var sourceHash = reader.ReadString();
            
            // Create downloads directory in current working directory
            var downloadsDir = Path.Combine(Environment.CurrentDirectory, "downloads");
            Directory.CreateDirectory(downloadsDir);
            
            // Create full save path
            var savePath = Path.Combine(downloadsDir, filename);
            Console.WriteLine($"Receiving file: {filename} ({filesize:N0} bytes)");
            Console.WriteLine($"Saving to: {savePath}");
            
            // Create directory if needed
            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            // Receive and save file
            using var fileStream = File.Create(savePath);
            var buffer = new byte[8192];
            var bytesRead = 0L;
            var sw = Stopwatch.StartNew();
            var lastUpdate = sw.ElapsedMilliseconds;
            var lastBytes = 0L;
            
            while (bytesRead < filesize)
            {
                var chunkSize = (int)Math.Min(buffer.Length, filesize - bytesRead);
                var read = stream.Read(buffer, 0, chunkSize);
                fileStream.Write(buffer, 0, read);
                bytesRead += read;
                
                // Update progress every 100ms
                var now = sw.ElapsedMilliseconds;
                if (now - lastUpdate >= 100)
                {
                    var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                    DisplayProgress(bytesRead, filesize, bytesPerSecond);
                    lastUpdate = now;
                    lastBytes = bytesRead;
                }
            }
            
            Console.WriteLine();
            fileStream.Close();
            
            // Verify hash
            var receivedHash = CalculateHash(savePath);
            if (sourceHash == receivedHash)
            {
                Console.WriteLine($"File received and verified: {savePath}");
            }
            else
            {
                Console.WriteLine($"Warning: File hash mismatch!");
                Console.WriteLine($"Expected: {sourceHash}");
                Console.WriteLine($"Received: {receivedHash}");
            }
            
            client.Close();
        }
    }

    static void SendFile(string host, string filepath)
    {
        if (!File.Exists(filepath))
        {
            Console.WriteLine($"File not found: {filepath}");
            return;
        }
        
        var fileInfo = new FileInfo(filepath);
        Console.WriteLine($"Sending {fileInfo.Name} ({fileInfo.Length:N0} bytes) to {host}");
        
        // Calculate hash before sending
        Console.Write("Calculating file hash... ");
        var hash = CalculateHash(filepath);
        Console.WriteLine("done");
        
        try
        {
            using var client = new TcpClient();
            client.Connect(host, Port);
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
                
                // Update progress every 100ms
                var now = sw.ElapsedMilliseconds;
                if (now - lastUpdate >= 100)
                {
                    var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                    DisplayProgress(bytesRead, fileInfo.Length, bytesPerSecond);
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
        }
    }
}
