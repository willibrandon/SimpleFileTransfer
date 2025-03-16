using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;

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

    static void RunServer()
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        
        var hostName = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(hostName)
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && 
                       ip.ToString().StartsWith("100."));
        
        Console.WriteLine($"Server started on port {Port}");
        Console.WriteLine("Tailscale IP Addresses:");
        foreach (var ip in addresses)
        {
            Console.WriteLine($"  {ip}");
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
            
            Console.WriteLine($"Receiving file: {filename} ({filesize} bytes)");
            
            // Create directory if needed
            var dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            // Receive and save file
            using var fileStream = File.Create(filename);
            var buffer = new byte[8192];
            var bytesRead = 0L;
            
            while (bytesRead < filesize)
            {
                var chunkSize = (int)Math.Min(buffer.Length, filesize - bytesRead);
                var read = stream.Read(buffer, 0, chunkSize);
                fileStream.Write(buffer, 0, read);
                bytesRead += read;
                
                // Show progress
                Console.Write($"\rReceived {bytesRead}/{filesize} bytes ({bytesRead * 100 / filesize}%)");
            }
            
            Console.WriteLine();
            Console.WriteLine($"File received: {filename}");
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
        Console.WriteLine($"Sending {fileInfo.Name} ({fileInfo.Length} bytes) to {host}");
        
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
            
            // Send file data
            using var fileStream = File.OpenRead(filepath);
            var buffer = new byte[8192];
            var bytesRead = 0L;
            int read;
            
            while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                stream.Write(buffer, 0, read);
                bytesRead += read;
                
                // Show progress
                Console.Write($"\rSent {bytesRead}/{fileInfo.Length} bytes ({bytesRead * 100 / fileInfo.Length}%)");
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
