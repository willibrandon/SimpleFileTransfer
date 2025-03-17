using Microsoft.AspNetCore.Mvc;
using SimpleFileTransfer.Services;
using System;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Controllers;

/// <summary>
/// API controller for handling file transfer operations.
/// </summary>
/// <remarks>
/// This controller provides an HTTP endpoint for transferring files with optional
/// compression and encryption. It uses the <see cref="IFileTransferService"/> to
/// perform the actual file transfer operations.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="TransferController"/> class.
/// </remarks>
/// <param name="fileTransferService">The file transfer service to use for file operations.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="fileTransferService"/> is null.</exception>
[ApiController]
[Route("api/[controller]")]
public class TransferController(IFileTransferService fileTransferService) : ControllerBase
{
    private readonly IFileTransferService _fileTransferService = fileTransferService
        ?? throw new ArgumentNullException(nameof(fileTransferService));

    /// <summary>
    /// Transfers a file from source to destination with optional compression and encryption.
    /// </summary>
    /// <param name="request">The transfer request containing source and destination paths and options.</param>
    /// <returns>A task representing the asynchronous operation with an action result.</returns>
    /// <response code="200">Returns when the file is successfully transferred.</response>
    /// <response code="400">Returns when the request is invalid (missing required fields).</response>
    /// <response code="500">Returns when an error occurs during the transfer.</response>
    [HttpPost]
    public async Task<IActionResult> TransferFile([FromBody] TransferRequest request)
    {
        if (string.IsNullOrEmpty(request.SourcePath) || string.IsNullOrEmpty(request.DestinationPath))
        {
            return BadRequest(new { error = "Source and destination paths are required" });
        }

        try
        {
            var options = new FileTransferOptions
            {
                SourcePath = request.SourcePath,
                DestinationPath = request.DestinationPath,
                Compress = request.Compress,
                Encrypt = request.Encrypt,
                Password = request.Password
            };

            await _fileTransferService.TransferFileAsync(options);
            
            return Ok(new { message = "File transferred successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
