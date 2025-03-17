namespace SimpleFileTransfer.Controllers;

/// <summary>
/// Represents a request to transfer a file with optional compression and encryption.
/// </summary>
/// <remarks>
/// This class is used as a data transfer object (DTO) for API requests to the
/// <see cref="TransferController"/>. It contains all the necessary information
/// to perform a file transfer operation.
/// </remarks>
public class TransferRequest
{
    /// <summary>
    /// Gets or sets the full path to the source file.
    /// </summary>
    /// <remarks>
    /// This property is required and cannot be null or empty.
    /// </remarks>
    public required string SourcePath { get; set; }

    /// <summary>
    /// Gets or sets the full path where the file should be copied to.
    /// </summary>
    /// <remarks>
    /// This property is required and cannot be null or empty.
    /// </remarks>
    public required string DestinationPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file should be compressed during transfer.
    /// </summary>
    /// <remarks>
    /// When set to true, the file will be compressed before being transferred.
    /// Default value is false.
    /// </remarks>
    public bool Compress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file should be encrypted during transfer.
    /// </summary>
    /// <remarks>
    /// When set to true, the file will be encrypted before being transferred.
    /// If encryption is enabled, a password must be provided.
    /// Default value is false.
    /// </remarks>
    public bool Encrypt { get; set; }

    /// <summary>
    /// Gets or sets the password to use for encryption.
    /// </summary>
    /// <remarks>
    /// This property is required when <see cref="Encrypt"/> is set to true.
    /// It can be null when encryption is not used.
    /// </remarks>
    public string? Password { get; set; }
    
    /// <summary>
    /// Gets or sets the speed limit for the transfer in KB/s.
    /// </summary>
    /// <remarks>
    /// A value of null or 0 means no limit.
    /// </remarks>
    public int? SpeedLimit { get; set; }
}
