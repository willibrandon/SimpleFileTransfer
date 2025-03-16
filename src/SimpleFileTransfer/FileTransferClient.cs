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
    private readonly bool _resumeEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferClient"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
    /// <param name="useCompression">Whether to use compression for file transfers. Defaults to false.</param>
    /// <param name="compressionAlgorithm">The compression algorithm to use. Defaults to GZip.</param>
    /// <param name="useEncryption">Whether to use encryption for file transfers. Defaults to false.</param>
    /// <param name="password">The password to use for encryption. Required if useEncryption is true.</param>
    /// <param name="resumeEnabled">Whether to enable resume capability for file transfers. Defaults to false.</param>
    public FileTransferClient(
        string host, 
        int port = Program.Port, 
        bool useCompression = false, 
        CompressionHelper.CompressionAlgorithm compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
        bool useEncryption = false,
        string? password = null,
        bool resumeEnabled = false)
    {
        _host = host;
        _port = port;
        _useCompression = useCompression;
        _compressionAlgorithm = compressionAlgorithm;
        _useEncryption = useEncryption;
        _password = password;
        _resumeEnabled = resumeEnabled;
        
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
        Console.WriteLine($"Resume capability: {(_resumeEnabled ? "Enabled" : "Disabled")}");
        
        // Calculate hash before sending
        Console.Write("Calculating file hash... ");
        var hash = FileOperations.CalculateHash(filepath);
        Console.WriteLine("done");
        
        // Check for resume information
        TransferResumeManager.ResumeInfo? resumeInfo = null;
        long resumePosition = 0;
        
        if (_resumeEnabled)
        {
            resumeInfo = TransferResumeManager.LoadResumeInfo(filepath);
            if (resumeInfo != null)
            {
                // Verify that the resume info matches the current transfer parameters
                if (resumeInfo.Hash == hash && 
                    resumeInfo.UseCompression == _useCompression &&
                    resumeInfo.UseEncryption == _useEncryption &&
                    resumeInfo.Host == _host &&
                    resumeInfo.Port == _port &&
                    (!_useCompression || resumeInfo.CompressionAlgorithm == _compressionAlgorithm))
                {
                    resumePosition = resumeInfo.BytesTransferred;
                    Console.WriteLine($"Resuming transfer from position {resumePosition:N0} bytes ({(double)resumePosition * 100 / fileInfo.Length:F2}%)");
                }
                else
                {
                    Console.WriteLine("Resume information found but parameters don't match. Starting a new transfer.");
                    resumeInfo = null;
                }
            }
        }
        
        // If no valid resume info, create a new one
        if (_resumeEnabled && resumeInfo == null)
        {
            resumeInfo = new TransferResumeManager.ResumeInfo
            {
                FilePath = filepath,
                FileName = Path.GetFileName(filepath),
                TotalSize = fileInfo.Length,
                BytesTransferred = 0,
                Hash = hash,
                UseCompression = _useCompression,
                CompressionAlgorithm = _compressionAlgorithm,
                UseEncryption = _useEncryption,
                Host = _host,
                Port = _port
            };
            
            TransferResumeManager.CreateResumeFile(resumeInfo);
        }
        
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
            // Send resume flag and position
            writer.Write(_resumeEnabled);
            writer.Write(resumePosition);
            
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
            
            // Calculate resume position in processed file
            long processedResumePosition = 0;
            if (resumePosition > 0)
            {
                // For simplicity, we use a proportional approach to estimate the position in the processed file
                processedResumePosition = (long)(processedSize * ((double)resumePosition / fileInfo.Length));
                Console.WriteLine($"Resuming from approximately {processedResumePosition:N0} bytes in the processed file");
            }
            
            // Send processed resume position
            writer.Write(processedResumePosition);
            
            // Send processed data
            using var processedDataStream = File.OpenRead(tempFile);
            
            // Skip to the resume position if resuming
            if (processedResumePosition > 0)
            {
                processedDataStream.Seek(processedResumePosition, SeekOrigin.Begin);
            }
            
            var buffer = new byte[8192];
            var bytesRead = processedResumePosition;
            var sw = Stopwatch.StartNew();
            var lastUpdate = sw.ElapsedMilliseconds;
            var lastBytes = bytesRead;
            int read;
            
            try
            {
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
                        
                        // Update resume info every second
                        if (_resumeEnabled && resumeInfo != null && now - resumeInfo.Timestamp.ToUniversalTime().Ticks / 10000 >= 1000)
                        {
                            // Calculate original file position based on processed position
                            var originalPosition = (long)(fileInfo.Length * ((double)bytesRead / processedSize));
                            resumeInfo.BytesTransferred = originalPosition;
                            TransferResumeManager.UpdateResumeFile(resumeInfo);
                        }
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("File sent successfully");
                
                // Delete resume file if transfer was successful
                if (_resumeEnabled && resumeInfo != null)
                {
                    TransferResumeManager.DeleteResumeFile(filepath);
                }
            }
            catch (IOException)
            {
                if (_resumeEnabled && resumeInfo != null)
                {
                    // Calculate original file position based on processed position
                    var originalPosition = (long)(fileInfo.Length * ((double)bytesRead / processedSize));
                    resumeInfo.BytesTransferred = originalPosition;
                    TransferResumeManager.UpdateResumeFile(resumeInfo);
                    
                    Console.WriteLine($"\nTransfer interrupted at {originalPosition:N0} bytes ({(double)originalPosition * 100 / fileInfo.Length:F2}%)");
                    Console.WriteLine("You can resume this transfer later using the 'resume' command.");
                    return;
                }
                
                throw;
            }
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
        Console.WriteLine($"Resume capability: {(_resumeEnabled ? "Enabled" : "Disabled")}");
        
        // For directory transfers, we handle resume at the individual file level
        // We'll keep track of which files have been successfully transferred
        var completedFiles = new HashSet<string>();
        
        // Check for resume information for this directory
        if (_resumeEnabled)
        {
            foreach (var file in files)
            {
                var resumeInfo = TransferResumeManager.LoadResumeInfo(file);
                if (resumeInfo != null && 
                    resumeInfo.BytesTransferred >= new FileInfo(file).Length &&
                    resumeInfo.DirectoryName == dirInfo.Name)
                {
                    // This file was already completely transferred
                    completedFiles.Add(file);
                }
            }
            
            if (completedFiles.Count > 0)
            {
                Console.WriteLine($"Found {completedFiles.Count} previously completed files that will be skipped.");
            }
        }
        
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
            // Send resume flag
            writer.Write(_resumeEnabled);
            
            if (_useCompression)
            {
                // Send compression algorithm
                writer.Write((int)_compressionAlgorithm);
            }
            
            // Send base directory name
            writer.Write(dirInfo.Name);
            
            // Calculate number of files to send (excluding completed ones)
            var filesToSend = files.Where(f => !completedFiles.Contains(f)).ToArray();
            
            // Send number of files
            writer.Write(filesToSend.Length);
            
            foreach (var (file, index) in filesToSend.Select((f, i) => (f, i)))
            {
                var fileInfo = new FileInfo(file);
                var relativePath = Path.GetRelativePath(dirPath, file);
                
                Console.WriteLine($"\nSending file {index + 1}/{filesToSend.Length}: {relativePath}");
                
                // Calculate hash
                var hash = FileOperations.CalculateHash(file);
                
                // Check for resume information
                TransferResumeManager.ResumeInfo? resumeInfo = null;
                long resumePosition = 0;
                
                if (_resumeEnabled)
                {
                    resumeInfo = TransferResumeManager.LoadResumeInfo(file);
                    if (resumeInfo != null)
                    {
                        // Verify that the resume info matches the current transfer parameters
                        if (resumeInfo.Hash == hash && 
                            resumeInfo.UseCompression == _useCompression &&
                            resumeInfo.UseEncryption == _useEncryption &&
                            resumeInfo.Host == _host &&
                            resumeInfo.Port == _port &&
                            resumeInfo.DirectoryName == dirInfo.Name &&
                            resumeInfo.RelativePath == relativePath &&
                            (!_useCompression || resumeInfo.CompressionAlgorithm == _compressionAlgorithm))
                        {
                            resumePosition = resumeInfo.BytesTransferred;
                            Console.WriteLine($"Resuming transfer from position {resumePosition:N0} bytes ({(double)resumePosition * 100 / fileInfo.Length:F2}%)");
                        }
                        else
                        {
                            Console.WriteLine("Resume information found but parameters don't match. Starting a new transfer.");
                            resumeInfo = null;
                        }
                    }
                }
                
                // If no valid resume info, create a new one
                if (_resumeEnabled && resumeInfo == null)
                {
                    resumeInfo = new TransferResumeManager.ResumeInfo
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        TotalSize = fileInfo.Length,
                        BytesTransferred = 0,
                        Hash = hash,
                        UseCompression = _useCompression,
                        CompressionAlgorithm = _compressionAlgorithm,
                        UseEncryption = _useEncryption,
                        Host = _host,
                        Port = _port,
                        DirectoryName = dirInfo.Name,
                        RelativePath = relativePath
                    };
                    
                    TransferResumeManager.CreateResumeFile(resumeInfo);
                }
                
                // Send relative path
                writer.Write(relativePath);
                // Send original filesize
                writer.Write(fileInfo.Length);
                // Send hash
                writer.Write(hash);
                // Send resume position
                writer.Write(resumePosition);
                
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
                
                // Calculate resume position in processed file
                long processedResumePosition = 0;
                if (resumePosition > 0)
                {
                    // For simplicity, we use a proportional approach to estimate the position in the processed file
                    processedResumePosition = (long)(processedSize * ((double)resumePosition / fileInfo.Length));
                    Console.WriteLine($"Resuming from approximately {processedResumePosition:N0} bytes in the processed file");
                }
                
                // Send processed resume position
                writer.Write(processedResumePosition);
                
                // Send processed data
                using var processedDataStream = File.OpenRead(tempFile);
                
                // Skip to the resume position if resuming
                if (processedResumePosition > 0)
                {
                    processedDataStream.Seek(processedResumePosition, SeekOrigin.Begin);
                }
                
                var buffer = new byte[8192];
                var bytesRead = processedResumePosition;
                var sw = Stopwatch.StartNew();
                var lastUpdate = sw.ElapsedMilliseconds;
                var lastBytes = bytesRead;
                int read;
                
                try
                {
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
                            
                            // Update resume info every second
                            if (_resumeEnabled && resumeInfo != null && now - resumeInfo.Timestamp.ToUniversalTime().Ticks / 10000 >= 1000)
                            {
                                // Calculate original file position based on processed position
                                var originalPosition = (long)(fileInfo.Length * ((double)bytesRead / processedSize));
                                resumeInfo.BytesTransferred = originalPosition;
                                TransferResumeManager.UpdateResumeFile(resumeInfo);
                            }
                        }
                    }
                    
                    Console.WriteLine();
                    
                    // Delete resume file if transfer was successful
                    if (_resumeEnabled && resumeInfo != null)
                    {
                        TransferResumeManager.DeleteResumeFile(file);
                    }
                }
                catch (IOException)
                {
                    if (_resumeEnabled && resumeInfo != null)
                    {
                        // Calculate original file position based on processed position
                        var originalPosition = (long)(fileInfo.Length * ((double)bytesRead / processedSize));
                        resumeInfo.BytesTransferred = originalPosition;
                        TransferResumeManager.UpdateResumeFile(resumeInfo);
                        
                        Console.WriteLine($"\nTransfer interrupted at {originalPosition:N0} bytes ({(double)originalPosition * 100 / fileInfo.Length:F2}%)");
                        Console.WriteLine("You can resume this transfer later using the 'resume' command.");
                        return;
                    }
                    
                    throw;
                }
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
    
    /// <summary>
    /// Lists all incomplete transfers that can be resumed.
    /// </summary>
    public static void ListResumableTransfers()
    {
        var resumeFiles = TransferResumeManager.GetAllResumeFiles();
        
        if (resumeFiles.Count == 0)
        {
            Console.WriteLine("No incomplete transfers found.");
            return;
        }
        
        Console.WriteLine($"Found {resumeFiles.Count} incomplete transfers:");
        Console.WriteLine();
        
        // Group multi-file transfers
        var multiFileGroups = resumeFiles
            .Where(r => r.IsMultiFile)
            .GroupBy(r => new { r.Host, r.Port })
            .ToList();
        
        // Process multi-file transfers first
        foreach (var group in multiFileGroups)
        {
            var firstFile = group.First();
            var validFiles = group.Where(r => File.Exists(r.FilePath)).ToList();
            var totalSize = validFiles.Sum(r => r.TotalSize);
            var totalTransferred = validFiles.Sum(r => r.BytesTransferred);
            var progress = totalSize > 0 ? (double)totalTransferred / totalSize * 100 : 0;
            
            // Find the index of the first file in the group
            var index = resumeFiles.IndexOf(firstFile) + 1;
            
            Console.WriteLine($"{index}. Multi-file transfer ({validFiles.Count} files, {totalSize:N0} bytes)");
            Console.WriteLine($"   Progress: {progress:F2}% ({totalTransferred:N0} of {totalSize:N0} bytes)");
            Console.WriteLine($"   Host: {firstFile.Host}:{firstFile.Port}");
            Console.WriteLine($"   Compression: {(firstFile.UseCompression ? $"Enabled ({firstFile.CompressionAlgorithm})" : "Disabled")}");
            Console.WriteLine($"   Encryption: {(firstFile.UseEncryption ? "Enabled" : "Disabled")}");
            Console.WriteLine($"   Last updated: {firstFile.Timestamp}");
            Console.WriteLine();
            
            // Skip individual listing of these files
            foreach (var file in group)
            {
                resumeFiles.Remove(file);
            }
            
            // Add back just the first one to maintain the index
            resumeFiles.Insert(index - 1, firstFile);
        }
        
        // Process remaining files
        for (int i = 0; i < resumeFiles.Count; i++)
        {
            var info = resumeFiles[i];
            
            // Skip if this is a multi-file transfer (already processed)
            if (info.IsMultiFile)
            {
                continue;
            }
            
            var progress = (double)info.BytesTransferred / info.TotalSize * 100;
            
            Console.WriteLine($"{i + 1}. {info.FileName} ({info.TotalSize:N0} bytes)");
            Console.WriteLine($"   Progress: {progress:F2}% ({info.BytesTransferred:N0} of {info.TotalSize:N0} bytes)");
            Console.WriteLine($"   Host: {info.Host}:{info.Port}");
            Console.WriteLine($"   Compression: {(info.UseCompression ? $"Enabled ({info.CompressionAlgorithm})" : "Disabled")}");
            Console.WriteLine($"   Encryption: {(info.UseEncryption ? "Enabled" : "Disabled")}");
            
            if (!string.IsNullOrEmpty(info.DirectoryName))
            {
                Console.WriteLine($"   Part of directory: {info.DirectoryName}");
                Console.WriteLine($"   Relative path: {info.RelativePath}");
            }
            
            Console.WriteLine($"   Last updated: {info.Timestamp}");
            Console.WriteLine();
        }
    }
    
    /// <summary>
    /// Resumes a specific transfer by index.
    /// </summary>
    /// <param name="index">The 1-based index of the transfer to resume.</param>
    /// <param name="password">The password to use for encryption, if the transfer is encrypted.</param>
    public static void ResumeTransfer(int index, string? password = null)
    {
        var resumeFiles = TransferResumeManager.GetAllResumeFiles();
        
        if (resumeFiles.Count == 0)
        {
            Console.WriteLine("No incomplete transfers found.");
            return;
        }
        
        if (index < 1 || index > resumeFiles.Count)
        {
            Console.WriteLine($"Invalid index. Please specify a number between 1 and {resumeFiles.Count}.");
            return;
        }
        
        var info = resumeFiles[index - 1];
        
        if (info.UseEncryption && string.IsNullOrEmpty(password))
        {
            Console.WriteLine("This transfer is encrypted. Please provide a password using the --password option.");
            return;
        }
        
        if (!File.Exists(info.FilePath))
        {
            Console.WriteLine($"The file {info.FilePath} no longer exists. Cannot resume transfer.");
            return;
        }
        
        var client = new FileTransferClient(
            info.Host,
            info.Port,
            info.UseCompression,
            info.CompressionAlgorithm,
            info.UseEncryption,
            password,
            true);
        
        if (!string.IsNullOrEmpty(info.DirectoryName))
        {
            // Part of a directory transfer
            // For simplicity, we'll just send the entire directory again
            // The client will skip files that have already been transferred
            var dirPath = Path.GetDirectoryName(info.FilePath);
            if (dirPath != null && Directory.Exists(dirPath))
            {
                client.SendDirectory(dirPath);
            }
            else
            {
                Console.WriteLine($"The directory containing {info.FilePath} no longer exists. Cannot resume transfer.");
            }
        }
        else if (info.IsMultiFile)
        {
            // Part of a multi-file transfer
            // Find all files that are part of the same multi-file transfer
            var multiFileTransfers = resumeFiles
                .Where(r => r.IsMultiFile && r.Host == info.Host && r.Port == info.Port)
                .ToList();
            
            // Filter out files that no longer exist
            var validFiles = multiFileTransfers
                .Where(r => File.Exists(r.FilePath))
                .Select(r => r.FilePath)
                .ToList();
            
            if (validFiles.Count == 0)
            {
                Console.WriteLine("No valid files found for this multi-file transfer. Cannot resume.");
                return;
            }
            
            Console.WriteLine($"Resuming multi-file transfer with {validFiles.Count} files");
            client.SendMultipleFiles(validFiles);
        }
        else
        {
            // Single file transfer
            client.SendFile(info.FilePath);
        }
    }

    /// <summary>
    /// Sends multiple files to the server.
    /// </summary>
    /// <param name="filePaths">A list of file paths to send.</param>
    /// <exception cref="ArgumentException">Thrown when the list of file paths is empty.</exception>
    public void SendMultipleFiles(List<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0)
        {
            throw new ArgumentException("No files to send.", nameof(filePaths));
        }
        
        var totalSize = filePaths.Sum(f => new FileInfo(f).Length);
        Console.WriteLine($"Sending {filePaths.Count} files ({totalSize:N0} bytes) to {_host}");
        Console.WriteLine($"Compression: {(_useCompression ? $"Enabled ({_compressionAlgorithm})" : "Disabled")}");
        Console.WriteLine($"Encryption: {(_useEncryption ? "Enabled" : "Disabled")}");
        Console.WriteLine($"Resume capability: {(_resumeEnabled ? "Enabled" : "Disabled")}");
        
        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();
            using var writer = new BinaryWriter(stream);
            
            // Send marker indicating this is a multi-file transfer
            writer.Write("MULTI:");
            // Send compression flag
            writer.Write(_useCompression);
            // Send encryption flag
            writer.Write(_useEncryption);
            // Send resume flag
            writer.Write(_resumeEnabled);
            
            if (_useCompression)
            {
                // Send compression algorithm
                writer.Write((int)_compressionAlgorithm);
            }
            
            // Send number of files
            writer.Write(filePaths.Count);
            
            foreach (var (filePath, index) in filePaths.Select((f, i) => (f, i)))
            {
                var fileInfo = new FileInfo(filePath);
                
                Console.WriteLine($"\nSending file {index + 1}/{filePaths.Count}: {fileInfo.Name}");
                
                // Calculate hash before sending
                Console.Write("Calculating file hash... ");
                var hash = FileOperations.CalculateHash(filePath);
                Console.WriteLine("done");
                
                // Check for resume information
                TransferResumeManager.ResumeInfo? resumeInfo = null;
                long resumePosition = 0;
                
                if (_resumeEnabled)
                {
                    resumeInfo = TransferResumeManager.LoadResumeInfo(filePath);
                    if (resumeInfo != null)
                    {
                        // Verify that the resume info matches the current transfer parameters
                        if (resumeInfo.Hash == hash && 
                            resumeInfo.UseCompression == _useCompression &&
                            resumeInfo.UseEncryption == _useEncryption &&
                            resumeInfo.Host == _host &&
                            resumeInfo.Port == _port &&
                            (!_useCompression || resumeInfo.CompressionAlgorithm == _compressionAlgorithm))
                        {
                            resumePosition = resumeInfo.BytesTransferred;
                            Console.WriteLine($"Resuming transfer from position {resumePosition:N0} bytes ({(double)resumePosition * 100 / fileInfo.Length:F2}%)");
                        }
                        else
                        {
                            Console.WriteLine("Resume information found but parameters don't match. Starting a new transfer.");
                            resumeInfo = null;
                        }
                    }
                }
                
                // If no valid resume info, create a new one
                if (_resumeEnabled && resumeInfo == null)
                {
                    resumeInfo = new TransferResumeManager.ResumeInfo
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        TotalSize = fileInfo.Length,
                        BytesTransferred = 0,
                        Hash = hash,
                        UseCompression = _useCompression,
                        CompressionAlgorithm = _compressionAlgorithm,
                        UseEncryption = _useEncryption,
                        Host = _host,
                        Port = _port,
                        IsMultiFile = true
                    };
                    
                    TransferResumeManager.CreateResumeFile(resumeInfo);
                }
                
                // Send filename
                writer.Write(Path.GetFileName(filePath));
                // Send original filesize
                writer.Write(fileInfo.Length);
                // Send hash
                writer.Write(hash);
                // Send resume position
                writer.Write(resumePosition);
                
                // Keep track of temporary files to clean up at the end
                var tempFiles = new List<string>();
                
                try
                {
                    // Send file data
                    using var fileStream = File.OpenRead(filePath);
                    
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
                    
                    // Calculate resume position in processed file
                    long processedResumePosition = 0;
                    if (resumePosition > 0)
                    {
                        // For simplicity, we use a proportional approach to estimate the position in the processed file
                        processedResumePosition = (long)(processedSize * ((double)resumePosition / fileInfo.Length));
                        Console.WriteLine($"Resuming from approximately {processedResumePosition:N0} bytes in the processed file");
                    }
                    
                    // Send processed resume position
                    writer.Write(processedResumePosition);
                    
                    // Send processed data
                    using var processedDataStream = File.OpenRead(tempFile);
                    
                    // Skip to the resume position if resuming
                    if (processedResumePosition > 0)
                    {
                        processedDataStream.Seek(processedResumePosition, SeekOrigin.Begin);
                    }
                    
                    var buffer = new byte[8192];
                    var bytesRead = processedResumePosition;
                    var sw = Stopwatch.StartNew();
                    var lastUpdate = sw.ElapsedMilliseconds;
                    var lastBytes = bytesRead;
                    int read;
                    
                    try
                    {
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
                                
                                // Update resume info every second
                                if (_resumeEnabled && resumeInfo != null && now - resumeInfo.Timestamp.ToUniversalTime().Ticks / 10000 >= 1000)
                                {
                                    // Calculate original file position based on processed position
                                    var originalPosition = (long)(fileInfo.Length * ((double)bytesRead / processedSize));
                                    resumeInfo.BytesTransferred = originalPosition;
                                    TransferResumeManager.UpdateResumeFile(resumeInfo);
                                }
                            }
                        }
                        
                        Console.WriteLine();
                        Console.WriteLine($"File {index + 1}/{filePaths.Count} sent successfully");
                        
                        // Delete resume file if transfer was successful
                        if (_resumeEnabled && resumeInfo != null)
                        {
                            TransferResumeManager.DeleteResumeFile(filePath);
                        }
                    }
                    catch (IOException)
                    {
                        if (_resumeEnabled && resumeInfo != null)
                        {
                            // Calculate original file position based on processed position
                            var originalPosition = (long)(fileInfo.Length * ((double)bytesRead / processedSize));
                            resumeInfo.BytesTransferred = originalPosition;
                            TransferResumeManager.UpdateResumeFile(resumeInfo);
                            
                            Console.WriteLine($"\nTransfer interrupted at {originalPosition:N0} bytes ({(double)originalPosition * 100 / fileInfo.Length:F2}%)");
                            Console.WriteLine("You can resume this transfer later using the 'resume' command.");
                            return;
                        }
                        
                        throw;
                    }
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
            
            Console.WriteLine("\nAll files sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }
}
