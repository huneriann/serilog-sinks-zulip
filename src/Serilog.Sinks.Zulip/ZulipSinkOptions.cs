namespace Serilog.Sinks.Zulip;

/// <summary>
/// Configuration options for the Zulip Sink.
/// </summary>
public sealed class ZulipSinkOptions
{
    /// <summary>
    /// The base URL of the Zulip server (e.g., https://your-org.zulipchat.com).
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// The email address of the bot user that will be used to send log messages.
    /// </summary>
    public string BotEmail { get; set; } = string.Empty;

    /// <summary>
    /// The API key for the bot user.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Channel (Stream) ID or Name to send logs to.
    /// </summary>
    public string Channel { get; set; } = "general";

    /// <summary>
    /// Default topic for the messages. If null, the Log Level will be used as the topic.
    /// </summary>
    public string? DefaultTopic { get; set; }
}