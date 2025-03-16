using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SimpleFileTransfer.Tests;

public class FileTransferTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _downloadDir;
    private Task? _serverTask;
    private CancellationTokenSource? _serverCts;

    public FileTransferTests()
    {
        // Create test directories
        _testDir = Path.Combine(Path.GetTempPath(), "SimpleFileTransferTests_" + Guid.NewGuid());
        _downloadDir = Path.Combine(_testDir, "downloads");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_downloadDir);
        Program.DownloadsDirectory = _downloadDir;
    }

    public void Dispose()
    {
        _serverCts?.Cancel();
        _serverTask?.Wait();
        Directory.Delete(_testDir, true);
        GC.SuppressFinalize(this);
    }

    private async Task StartServer()
    {
        _serverCts = new CancellationTokenSource();
        _serverTask = Task.Run(() =>
        {
            // Redirect console output to StringWriter to avoid polluting test output
            var originalOut = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);

            try
            {
                var server = new FileTransferServer(_downloadDir);
                server.Start(_serverCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when we cancel the server
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        });

        // Give the server a moment to start
        await Task.Delay(100);
    }

    private string CreateTestFile(string filename, string content)
    {
        var path = Path.Combine(_testDir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateTestDirectory(string dirname, Dictionary<string, string> files)
    {
        var dirPath = Path.Combine(_testDir, dirname);
        Directory.CreateDirectory(dirPath);

        foreach (var (file, content) in files)
        {
            var filePath = Path.Combine(dirPath, file);
            var fileDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }
            File.WriteAllText(filePath, content);
        }

        return dirPath;
    }

    private string CalculateHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    [Fact]
    public async Task SingleFileTransfer_Success()
    {
        // Arrange
        var content = "Hello, World!";
        var testFile = CreateTestFile("test.txt", content);
        await StartServer();

        // Act
        var client = new FileTransferClient("localhost");
        client.SendFile(testFile);
        await Task.Delay(1000); // Give time for transfer to complete

        // Assert
        var downloadedFile = Path.Combine(_downloadDir, "test.txt");
        Assert.True(File.Exists(downloadedFile));
        Assert.Equal(content, File.ReadAllText(downloadedFile));
    }

    [Fact]
    public async Task DirectoryTransfer_Success()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            { "file1.txt", "Content 1" },
            { "subdir/file2.txt", "Content 2" },
            { "subdir/deeper/file3.txt", "Content 3" }
        };
        var testDir = CreateTestDirectory("testdir", files);
        await StartServer();

        // Act
        var client = new FileTransferClient("localhost");
        client.SendDirectory(testDir);
        await Task.Delay(2000); // Give time for transfer to complete

        // Assert
        foreach (var (file, content) in files)
        {
            var downloadedFile = Path.Combine(_downloadDir, "testdir", file);
            Assert.True(File.Exists(downloadedFile), $"File should exist: {file}");
            Assert.Equal(content, File.ReadAllText(downloadedFile));
        }
    }

    [Fact]
    public async Task LargeFileTransfer_Success()
    {
        // Arrange
        var content = new string('X', 1024 * 1024); // 1MB of data
        var testFile = CreateTestFile("large.txt", content);
        await StartServer();

        // Act
        var client = new FileTransferClient("localhost");
        client.SendFile(testFile);
        await Task.Delay(2000); // Give more time for large file

        // Assert
        var downloadedFile = Path.Combine(_downloadDir, "large.txt");
        Assert.True(File.Exists(downloadedFile));
        Assert.Equal(content, File.ReadAllText(downloadedFile));
    }

    [Fact]
    public async Task FileHashVerification_Success()
    {
        // Arrange
        var content = "Test content for hash verification";
        var testFile = CreateTestFile("hash_test.txt", content);
        var expectedHash = CalculateHash(content);
        await StartServer();

        // Act
        var client = new FileTransferClient("localhost");
        client.SendFile(testFile);
        await Task.Delay(1000);

        // Assert
        var downloadedFile = Path.Combine(_downloadDir, "hash_test.txt");
        Assert.True(File.Exists(downloadedFile));
        var downloadedContent = File.ReadAllText(downloadedFile);
        var actualHash = CalculateHash(downloadedContent);
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public async Task NonExistentFile_ThrowsError()
    {
        // Arrange
        await StartServer();

        // Act & Assert
        var nonExistentFile = Path.Combine(_testDir, "nonexistent.txt");
        var client = new FileTransferClient("localhost");
        Assert.Throws<FileNotFoundException>(() => client.SendFile(nonExistentFile));
    }
} 