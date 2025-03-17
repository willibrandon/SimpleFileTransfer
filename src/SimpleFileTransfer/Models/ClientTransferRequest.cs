using Microsoft.AspNetCore.Http;
using SimpleFileTransfer.Helpers;

namespace SimpleFileTransfer.Models;

/// <summary>
/// Represents a request to transfer a file from a client to a server.
/// </summary>
public class ClientTransferRequest
{
    /// <summary>
    /// Gets or sets the file to transfer.
    /// </summary>
    public IFormFile? File { get; set; }
    
    /// <summary>
    /// Gets or sets the filename to use for the transferred file.
    /// </summary>
    public string? FileName { get; set; }
    
    /// <summary>
    /// Gets or sets the hostname or IP address of the server.
    /// </summary>
    public string Host { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the port number of the server.
    /// </summary>
    public int Port { get; set; } = SimpleFileTransfer.Program.Port;
    
    /// <summary>
    /// Gets or sets a value indicating whether to use compression.
    /// </summary>
    public bool UseCompression { get; set; }
    
    /// <summary>
    /// Gets or sets the compression algorithm to use.
    /// </summary>
    public CompressionHelper.CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionHelper.CompressionAlgorithm.GZip;
    
    /// <summary>
    /// Gets or sets a value indicating whether to use encryption.
    /// </summary>
    public bool UseEncryption { get; set; }
    
    /// <summary>
    /// Gets or sets the password to use for encryption.
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to enable resume capability.
    /// </summary>
    public bool ResumeEnabled { get; set; }
    
    /// <summary>
    /// Gets or sets the speed limit for the transfer in KB/s.
    /// </summary>
    /// <remarks>
    /// A value of null or 0 means no limit.
    /// </remarks>
    public int? SpeedLimit { get; set; }
} 