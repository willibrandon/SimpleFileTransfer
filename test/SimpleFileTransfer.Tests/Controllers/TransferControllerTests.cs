using Microsoft.AspNetCore.Mvc;
using Moq;
using SimpleFileTransfer.Controllers;
using SimpleFileTransfer.Services;

namespace SimpleFileTransfer.Tests.Controllers;

public class TransferControllerTests
{
    private readonly Mock<IFileTransferService> _mockFileTransferService;
    private readonly TransferController _controller;

    public TransferControllerTests()
    {
        _mockFileTransferService = new Mock<IFileTransferService>();
        _controller = new TransferController(_mockFileTransferService.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFileTransferServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TransferController(null!));
    }

    [Fact]
    public async Task TransferFile_ReturnsBadRequest_WhenSourcePathIsEmpty()
    {
        // Arrange
        var request = new TransferRequest
        {
            SourcePath = "",
            DestinationPath = "destination.txt"
        };

        // Act
        var result = await _controller.TransferFile(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<object>(badRequestResult.Value, false);
        Assert.Contains("error", errorResponse.ToString()!);
    }

    [Fact]
    public async Task TransferFile_ReturnsBadRequest_WhenDestinationPathIsEmpty()
    {
        // Arrange
        var request = new TransferRequest
        {
            SourcePath = "source.txt",
            DestinationPath = ""
        };

        // Act
        var result = await _controller.TransferFile(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<object>(badRequestResult.Value, false);
        Assert.Contains("error", errorResponse.ToString()!);
    }

    [Fact]
    public async Task TransferFile_ReturnsOk_WhenTransferSucceeds()
    {
        // Arrange
        var request = new TransferRequest
        {
            SourcePath = "source.txt",
            DestinationPath = "destination.txt",
            Compress = true,
            Encrypt = true,
            Password = "password123"
        };

        _mockFileTransferService
            .Setup(s => s.TransferFileAsync(It.IsAny<FileTransferOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TransferFile(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var successResponse = Assert.IsType<object>(okResult.Value, false);
        Assert.Contains("success", successResponse.ToString()!);

        // Verify service was called with correct options
        _mockFileTransferService.Verify(s => s.TransferFileAsync(It.Is<FileTransferOptions>(
            o => o.SourcePath == request.SourcePath &&
                 o.DestinationPath == request.DestinationPath &&
                 o.Compress == request.Compress &&
                 o.Encrypt == request.Encrypt &&
                 o.Password == request.Password
        )), Times.Once);
    }

    [Fact]
    public async Task TransferFile_ReturnsServerError_WhenExceptionOccurs()
    {
        // Arrange
        var request = new TransferRequest
        {
            SourcePath = "source.txt",
            DestinationPath = "destination.txt"
        };

        var expectedException = new IOException("Test exception");
        _mockFileTransferService
            .Setup(s => s.TransferFileAsync(It.IsAny<FileTransferOptions>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _controller.TransferFile(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        
        var errorResponse = Assert.IsType<object>(statusCodeResult.Value, false);
        Assert.Contains(expectedException.Message, errorResponse.ToString()!);
    }

    [Fact]
    public async Task TransferFile_MapsRequestToOptions_Correctly()
    {
        // Arrange
        var request = new TransferRequest
        {
            SourcePath = "source.txt",
            DestinationPath = "destination.txt",
            Compress = true,
            Encrypt = true,
            Password = "password123"
        };

        FileTransferOptions? capturedOptions = null;
        _mockFileTransferService
            .Setup(s => s.TransferFileAsync(It.IsAny<FileTransferOptions>()))
            .Callback<FileTransferOptions>(options => capturedOptions = options)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.TransferFile(request);

        // Assert
        Assert.NotNull(capturedOptions);
        Assert.Equal(request.SourcePath, capturedOptions.SourcePath);
        Assert.Equal(request.DestinationPath, capturedOptions.DestinationPath);
        Assert.Equal(request.Compress, capturedOptions.Compress);
        Assert.Equal(request.Encrypt, capturedOptions.Encrypt);
        Assert.Equal(request.Password, capturedOptions.Password);
    }

    [Fact]
    public async Task TransferFile_ReturnsBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.TransferFile(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<object>(badRequestResult.Value, false);
        Assert.Contains("error", errorResponse.ToString()!);
    }
}
