namespace Serilog.Sinks.Zulip;

using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Sinks.PeriodicBatching;
using System;

/// <summary>
/// Extension methods to configure the Zulip sink.
/// </summary>
public static class ZulipLoggerConfigurationExtensions
{
    private const string DefaultOutputTemplate = "**[{Level}]** {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Adds a Zulip sink to the logger configuration with simple string parameters.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The logger configuration to modify.</param>
    /// <param name="serverUrl">The base URL of the Zulip server (e.g., https://your-org.zulipchat.com).</param>
    /// <param name="channel">The channel (stream) ID or name to send logs to.</param>
    /// <param name="botEmail">The email address of the bot user.</param>
    /// <param name="apiKey">The API key for the bot user.</param>
    /// <param name="defaultTopic">Default topic for messages. If null, the log level is used as the topic.</param>
    /// <param name="outputTemplate">A message template describing the format of each log event.
    /// If null, the default Zulip-friendly template is used.</param>
    /// <param name="formatProvider">An optional format provider for formatting property values.</param>
    /// <param name="restrictedToMinimumLevel">The minimum log event level required to pass through the sink.</param>
    /// <param name="batchSizeLimit">Maximum number of events to include in a single batch.</param>
    /// <param name="periodSeconds">Time to wait between sending batches (in seconds).</param>
    /// <returns>The logger configuration with the Zulip sink added.</returns>
    public static LoggerConfiguration Zulip(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        string serverUrl,
        string channel,
        string botEmail,
        string apiKey,
        string? defaultTopic = null,
        string? outputTemplate = null,
        IFormatProvider? formatProvider = null,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Debug,
        int batchSizeLimit = 50,
        int periodSeconds = 2)
    {
        var options = new ZulipSinkOptions
        {
            ServerUrl = serverUrl,
            Channel = channel,
            BotEmail = botEmail,
            ApiKey = apiKey,
            DefaultTopic = defaultTopic
        };

        // Create the formatter based on the provided template or use the default
        var formatter = new MessageTemplateTextFormatter(
            outputTemplate ?? DefaultOutputTemplate,
            formatProvider);

        return loggerSinkConfiguration.Zulip(options, formatter, restrictedToMinimumLevel, batchSizeLimit,
            periodSeconds);
    }

    /// <summary>
    /// Adds a Zulip sink to the logger configuration using the supplied options and formatter.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The logger configuration to modify.</param>
    /// <param name="options">The configuration options for the Zulip sink.</param>
    /// <param name="formatter">The formatter used to render each log event.
    /// If null, the default Zulip template is used.</param>
    /// <param name="restrictedToMinimumLevel">The minimum log event level required to pass through the sink.</param>
    /// <param name="batchSizeLimit">Maximum number of events to include in a single batch.</param>
    /// <param name="periodSeconds">Time to wait between sending batches (in seconds).</param>
    /// <returns>The logger configuration with the Zulip sink added.</returns>
    public static LoggerConfiguration Zulip(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        ZulipSinkOptions options,
        ITextFormatter? formatter = null,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Debug,
        int batchSizeLimit = 50,
        int periodSeconds = 2)
    {
        if (loggerSinkConfiguration is null)
            throw new ArgumentNullException(nameof(loggerSinkConfiguration));

        if (options is null)
            throw new ArgumentNullException(nameof(options));

        var batchingOptions = new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = batchSizeLimit,
            Period = TimeSpan.FromSeconds(periodSeconds),
            EagerlyEmitFirstEvent = true,
            QueueLimit = 5000
        };

        formatter ??= new MessageTemplateTextFormatter(DefaultOutputTemplate, null);

        return loggerSinkConfiguration.Zulip(options, batchingOptions, formatter, restrictedToMinimumLevel);
    }

    /// <summary>
    /// Adds a Zulip sink with full control over batching options.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The logger configuration to modify.</param>
    /// <param name="options">The configuration options for the Zulip sink.</param>
    /// <param name="batchingOptions">The periodic batching configuration.</param>
    /// <param name="formatter">The formatter used to render each log event.</param>
    /// <param name="restrictedToMinimumLevel">The minimum log event level required to pass through the sink.</param>
    /// <returns>The logger configuration with the Zulip sink added.</returns>
    public static LoggerConfiguration Zulip(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        ZulipSinkOptions options,
        PeriodicBatchingSinkOptions batchingOptions,
        ITextFormatter formatter,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Debug)
    {
        if (loggerSinkConfiguration is null)
            throw new ArgumentNullException(nameof(loggerSinkConfiguration));
        
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        
        if (batchingOptions is null)
            throw new ArgumentNullException(nameof(batchingOptions));
        
        if (formatter is null)
            throw new ArgumentNullException(nameof(formatter));

        var zulipSink = new ZulipBatchedSink(options, formatter);
        var batchingSink = new PeriodicBatchingSink(zulipSink, batchingOptions);

        return loggerSinkConfiguration.Sink(batchingSink, restrictedToMinimumLevel);
    }
}