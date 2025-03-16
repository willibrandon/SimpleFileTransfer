namespace SimpleFileTransfer.Tests;

public class TransferQueueTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _downloadDir;
    private Task? _serverTask;
    private CancellationTokenSource? _serverCts;
    private const string TestPassword = "testpassword123";
    private static int _portCounter = 9900; // Start from a different port than FileTransferTests

    public TransferQueueTests()
    {
        // Create test directories
        _testDir = Path.Combine(Path.GetTempPath(), "SimpleFileTransferQueueTests_" + Guid.NewGuid());
        _downloadDir = Path.Combine(_testDir, "downloads");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_downloadDir);
        Program.DownloadsDirectory = _downloadDir;
    }

    public void Dispose()
    {
        _serverCts?.Cancel();
        _serverTask?.Wait();
        
        // Clean up test directories
        try
        {
            Directory.Delete(_testDir, true);
        }
        catch
        {
            // Ignore errors during cleanup
        }
        
        GC.SuppressFinalize(this);
    }

    private async Task<int> StartServer(string? password = null)
    {
        // Use a different port for each test
        int port = Interlocked.Increment(ref _portCounter);
        
        _serverCts = new CancellationTokenSource();
        _serverTask = Task.Run(() =>
        {
            // Redirect console output to StringWriter to avoid polluting test output
            var originalOut = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);

            try
            {
                var server = new FileTransferServer(_downloadDir, port, password, _serverCts.Token);
                server.Start();
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
        
        return port;
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

    [Fact]
    public async Task QueueSingleFile_Success()
    {
        // Arrange
        var content = "Queue test content";
        var testFile = CreateTestFile("queue_test.txt", content);
        var port = await StartServer();

        // Create a new queue for this test
        var queue = new TransferQueue();
        var transferCompleted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            transferCompleted.SetResult(args.Success);
        };

        // Act
        var transfer = new QueuedFileTransfer("localhost", testFile, port: port);
        queue.Enqueue(transfer);
        queue.Start();

        // Wait for the transfer to complete with a timeout
        var completed = await Task.WhenAny(transferCompleted.Task, Task.Delay(5000));
        var success = completed == transferCompleted.Task && await transferCompleted.Task;
        
        // Add a delay to give the server time to process the file
        await Task.Delay(1000);

        // Assert
        var downloadedFile = Path.Combine(_downloadDir, "queue_test.txt");
        Assert.True(success, "Transfer should complete successfully");
        Assert.True(File.Exists(downloadedFile), "File should be downloaded");
        Assert.Equal(content, File.ReadAllText(downloadedFile));
    }

    [Fact]
    public async Task QueueMultipleFiles_ProcessedSequentially()
    {
        // Arrange
        var port = await StartServer();
        var file1 = CreateTestFile("queue_file1.txt", "Queue file 1 content");
        var file2 = CreateTestFile("queue_file2.txt", "Queue file 2 content");
        var file3 = CreateTestFile("queue_file3.txt", "Queue file 3 content");

        // Create a new queue for this test
        var queue = new TransferQueue();
        var completedTransfers = new List<string>();
        var allCompleted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            lock (completedTransfers)
            {
                completedTransfers.Add(args.Transfer.Description);
            }
        };
        
        queue.AllTransfersCompleted += (sender, args) => 
        {
            allCompleted.SetResult(true);
        };

        // Act
        queue.Enqueue(new QueuedFileTransfer("localhost", file1, port: port));
        queue.Enqueue(new QueuedFileTransfer("localhost", file2, port: port));
        queue.Enqueue(new QueuedFileTransfer("localhost", file3, port: port));
        
        Assert.Equal(3, queue.Count);
        
        queue.Start();

        // Wait for all transfers to complete with a timeout
        var completed = await Task.WhenAny(allCompleted.Task, Task.Delay(10000));
        var success = completed == allCompleted.Task && await allCompleted.Task;
        
        // Add a delay to give the server time to process the files
        await Task.Delay(1000);

        // Assert
        Assert.True(success, "All transfers should complete successfully");
        Assert.Equal(3, completedTransfers.Count);
        
        // Check that all files were downloaded
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_file1.txt")));
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_file2.txt")));
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_file3.txt")));
        
        // Check file contents
        Assert.Equal("Queue file 1 content", File.ReadAllText(Path.Combine(_downloadDir, "queue_file1.txt")));
        Assert.Equal("Queue file 2 content", File.ReadAllText(Path.Combine(_downloadDir, "queue_file2.txt")));
        Assert.Equal("Queue file 3 content", File.ReadAllText(Path.Combine(_downloadDir, "queue_file3.txt")));
    }

    [Fact]
    public async Task QueueDirectory_Success()
    {
        // Arrange
        var port = await StartServer();
        var files = new Dictionary<string, string>
        {
            { "file1.txt", "Directory queue test 1" },
            { "subdir/file2.txt", "Directory queue test 2" },
            { "subdir/deeper/file3.txt", "Directory queue test 3" }
        };
        var testDir = CreateTestDirectory("queue_dir", files);

        // Create a new queue for this test
        var queue = new TransferQueue();
        var transferCompleted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            transferCompleted.SetResult(args.Success);
        };

        // Act
        var transfer = new QueuedDirectoryTransfer("localhost", testDir, port: port);
        queue.Enqueue(transfer);
        queue.Start();

        // Wait for the transfer to complete with a timeout
        var completed = await Task.WhenAny(transferCompleted.Task, Task.Delay(5000));
        var success = completed == transferCompleted.Task && await transferCompleted.Task;
        
        // Add a delay to give the server time to process the files
        await Task.Delay(1000);

        // Assert
        Assert.True(success, "Transfer should complete successfully");
        
        // Check that all files were downloaded
        foreach (var (file, content) in files)
        {
            var downloadedFile = Path.Combine(_downloadDir, "queue_dir", file);
            Assert.True(File.Exists(downloadedFile), $"File should exist: {file}");
            Assert.Equal(content, File.ReadAllText(downloadedFile));
        }
    }

    [Fact]
    public async Task QueueMultiFileTransfer_Success()
    {
        // Arrange
        var port = await StartServer();
        var file1 = CreateTestFile("queue_multi1.txt", "Queue multi-file 1");
        var file2 = CreateTestFile("queue_multi2.txt", "Queue multi-file 2");
        var file3 = CreateTestFile("queue_multi3.txt", "Queue multi-file 3");
        
        var files = new List<string> { file1, file2, file3 };

        // Create a new queue for this test
        var queue = new TransferQueue();
        var transferCompleted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            transferCompleted.SetResult(args.Success);
        };

        // Act
        var transfer = new QueuedMultiFileTransfer("localhost", files, port: port);
        queue.Enqueue(transfer);
        queue.Start();

        // Wait for the transfer to complete with a timeout
        var completed = await Task.WhenAny(transferCompleted.Task, Task.Delay(5000));
        var success = completed == transferCompleted.Task && await transferCompleted.Task;
        
        // Add a delay to give the server time to process the files
        await Task.Delay(1000);

        // Assert
        Assert.True(success, "Transfer should complete successfully");
        
        // Check that all files were downloaded
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_multi1.txt")));
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_multi2.txt")));
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_multi3.txt")));
        
        // Check file contents
        Assert.Equal("Queue multi-file 1", File.ReadAllText(Path.Combine(_downloadDir, "queue_multi1.txt")));
        Assert.Equal("Queue multi-file 2", File.ReadAllText(Path.Combine(_downloadDir, "queue_multi2.txt")));
        Assert.Equal("Queue multi-file 3", File.ReadAllText(Path.Combine(_downloadDir, "queue_multi3.txt")));
    }

    [Fact]
    public async Task QueueWithCompression_Success()
    {
        // Arrange
        var port = await StartServer();
        var content = new string('X', 10000); // Content that should compress well
        var testFile = CreateTestFile("queue_compressed.txt", content);

        // Create a new queue for this test
        var queue = new TransferQueue();
        var transferCompleted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            transferCompleted.SetResult(args.Success);
        };

        // Act
        var transfer = new QueuedFileTransfer(
            "localhost", 
            testFile, 
            useCompression: true, 
            compressionAlgorithm: CompressionHelper.CompressionAlgorithm.GZip,
            port: port);
            
        queue.Enqueue(transfer);
        queue.Start();

        // Wait for the transfer to complete with a timeout
        var completed = await Task.WhenAny(transferCompleted.Task, Task.Delay(5000));
        var success = completed == transferCompleted.Task && await transferCompleted.Task;
        
        // Add a delay to give the server time to process the file
        await Task.Delay(1000);

        // Assert
        Assert.True(success, "Transfer should complete successfully");
        var downloadedFile = Path.Combine(_downloadDir, "queue_compressed.txt");
        Assert.True(File.Exists(downloadedFile), "File should be downloaded");
        Assert.Equal(content, File.ReadAllText(downloadedFile));
    }

    [Fact]
    public async Task QueueWithEncryption_Success()
    {
        // Arrange
        var port = await StartServer(TestPassword);
        var content = "Encrypted queue test content";
        var testFile = CreateTestFile("queue_encrypted.txt", content);

        // Create a new queue for this test
        var queue = new TransferQueue();
        var transferCompleted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            transferCompleted.SetResult(args.Success);
        };

        // Act
        var transfer = new QueuedFileTransfer(
            "localhost", 
            testFile, 
            useEncryption: true, 
            password: TestPassword,
            port: port);
            
        queue.Enqueue(transfer);
        queue.Start();

        // Wait for the transfer to complete with a timeout
        var completed = await Task.WhenAny(transferCompleted.Task, Task.Delay(5000));
        var success = completed == transferCompleted.Task && await transferCompleted.Task;
        
        // Add a delay to give the server time to process the file
        await Task.Delay(1000);

        // Assert
        Assert.True(success, "Transfer should complete successfully");
        var downloadedFile = Path.Combine(_downloadDir, "queue_encrypted.txt");
        Assert.True(File.Exists(downloadedFile), "File should be downloaded");
        Assert.Equal(content, File.ReadAllText(downloadedFile));
    }

    [Fact]
    public async Task QueueStop_CancelsProcessing()
    {
        // Arrange
        var port = await StartServer();
        var file1 = CreateTestFile("queue_stop1.txt", "Queue stop test 1");
        var file2 = CreateTestFile("queue_stop2.txt", "Queue stop test 2");
        var file3 = CreateTestFile("queue_stop3.txt", "Queue stop test 3");

        // Create a new queue for this test
        var queue = new TransferQueue();
        var transferStarted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            // Signal that at least one transfer has completed
            transferStarted.TrySetResult(true);
        };

        // Act
        queue.Enqueue(new QueuedFileTransfer("localhost", file1, port: port));
        queue.Enqueue(new QueuedFileTransfer("localhost", file2, port: port));
        queue.Enqueue(new QueuedFileTransfer("localhost", file3, port: port));
        
        Assert.Equal(3, queue.Count);
        
        queue.Start();

        // Wait for the first transfer to complete
        await Task.WhenAny(transferStarted.Task, Task.Delay(5000));
        
        // Stop the queue
        queue.Stop();
        
        // Wait a moment for the stop to take effect
        await Task.Delay(500);
        
        // Assert
        Assert.False(queue.IsProcessing, "Queue should not be processing after stop");
        
        // At least one file should be downloaded, but not all three
        var downloadedCount = new[]
        {
            File.Exists(Path.Combine(_downloadDir, "queue_stop1.txt")),
            File.Exists(Path.Combine(_downloadDir, "queue_stop2.txt")),
            File.Exists(Path.Combine(_downloadDir, "queue_stop3.txt"))
        }.Count(exists => exists);
        
        Assert.True(downloadedCount > 0 && downloadedCount < 3, 
            $"Expected some but not all files to be downloaded, got {downloadedCount}");
    }

    [Fact]
    public void QueueClear_RemovesAllTransfers()
    {
        // Arrange
        var file1 = CreateTestFile("queue_clear1.txt", "Queue clear test 1");
        var file2 = CreateTestFile("queue_clear2.txt", "Queue clear test 2");
        var file3 = CreateTestFile("queue_clear3.txt", "Queue clear test 3");

        // Create a new queue for this test
        var queue = new TransferQueue();

        // Act
        queue.Enqueue(new QueuedFileTransfer("localhost", file1));
        queue.Enqueue(new QueuedFileTransfer("localhost", file2));
        queue.Enqueue(new QueuedFileTransfer("localhost", file3));
        
        Assert.Equal(3, queue.Count);
        
        queue.Clear();
        
        // Assert
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task MixedTransferTypes_ProcessedSequentially()
    {
        // Arrange
        var port = await StartServer();
        
        // Create test files
        var singleFile = CreateTestFile("queue_mixed_single.txt", "Queue mixed single file");
        
        var multiFiles = new List<string>
        {
            CreateTestFile("queue_mixed_multi1.txt", "Queue mixed multi-file 1"),
            CreateTestFile("queue_mixed_multi2.txt", "Queue mixed multi-file 2")
        };
        
        var dirFiles = new Dictionary<string, string>
        {
            { "file1.txt", "Queue mixed dir file 1" },
            { "subdir/file2.txt", "Queue mixed dir file 2" }
        };
        var dirPath = CreateTestDirectory("queue_mixed_dir", dirFiles);

        // Create a new queue for this test
        var queue = new TransferQueue();
        var completedTransfers = new List<string>();
        var allCompleted = new TaskCompletionSource<bool>();
        
        queue.TransferCompleted += (sender, args) => 
        {
            lock (completedTransfers)
            {
                completedTransfers.Add(args.Transfer.Description);
            }
        };
        
        queue.AllTransfersCompleted += (sender, args) => 
        {
            allCompleted.SetResult(true);
        };

        // Act - add different types of transfers
        queue.Enqueue(new QueuedFileTransfer("localhost", singleFile, port: port));
        queue.Enqueue(new QueuedMultiFileTransfer("localhost", multiFiles, port: port));
        queue.Enqueue(new QueuedDirectoryTransfer("localhost", dirPath, port: port));
        
        Assert.Equal(3, queue.Count);
        
        queue.Start();

        // Wait for all transfers to complete with a timeout
        var completed = await Task.WhenAny(allCompleted.Task, Task.Delay(10000));
        var success = completed == allCompleted.Task && await allCompleted.Task;
        
        // Add a delay to give the server time to process the files
        await Task.Delay(1000);

        // Assert
        Assert.True(success, "All transfers should complete successfully");
        Assert.Equal(3, completedTransfers.Count);
        
        // Check that all files were downloaded
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_mixed_single.txt")));
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_mixed_multi1.txt")));
        Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_mixed_multi2.txt")));
        
        foreach (var (file, _) in dirFiles)
        {
            Assert.True(File.Exists(Path.Combine(_downloadDir, "queue_mixed_dir", file)));
        }
        
        // Check file contents
        Assert.Equal("Queue mixed single file", File.ReadAllText(Path.Combine(_downloadDir, "queue_mixed_single.txt")));
        Assert.Equal("Queue mixed multi-file 1", File.ReadAllText(Path.Combine(_downloadDir, "queue_mixed_multi1.txt")));
        Assert.Equal("Queue mixed multi-file 2", File.ReadAllText(Path.Combine(_downloadDir, "queue_mixed_multi2.txt")));
        
        foreach (var (file, content) in dirFiles)
        {
            Assert.Equal(content, File.ReadAllText(Path.Combine(_downloadDir, "queue_mixed_dir", file)));
        }
    }
}
