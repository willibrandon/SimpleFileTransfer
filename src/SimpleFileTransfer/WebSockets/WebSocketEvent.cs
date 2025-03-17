using System.Text.Json.Serialization;

namespace SimpleFileTransfer.WebSockets;

/// <summary>
/// Represents an event sent over WebSockets.
/// </summary>
public class WebSocketEvent
{
    /// <summary>
    /// Gets or sets the type of the event.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the data associated with the event.
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
} 