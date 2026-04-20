namespace Serilog.Sinks.Zulip;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.PeriodicBatching;

/// <summary>
/// Batched sink that sends log events to a Zulip channel (stream) using the Zulip REST API.
/// </summary>
/// <remarks>
/// Each batch of log events is grouped by topic and sent as a single Zulip message.
/// The supplied <see cref="ITextFormatter"/> is used to render each log event.
/// </remarks>
public sealed class ZulipBatchedSink : IBatchedLogEventSink, IDisposable
{
    /// <summary>
    /// The HTTP client used to send requests to the Zulip REST API.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// The configuration options dictating the target server, credentials, and routing.
    /// </summary>
    private readonly ZulipSinkOptions _options;

    /// <summary>
    /// The text formatter used to transform Serilog <see cref="LogEvent"/> instances into formatted strings.
    /// </summary>
    private readonly ITextFormatter _formatter;

    /// <summary>
    /// The relative API endpoint path for sending messages in Zulip.
    /// </summary>
    private const string ApiPath = "api/v1/messages";

    /// <summary>
    /// Initializes a new instance of the <see cref="ZulipBatchedSink"/> class.
    /// </summary>
    /// <param name="options">The configuration options for the Zulip sink.</param>
    /// <param name="formatter">The formatter used to render each log event into text.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="formatter"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when any required option (<c>ServerUrl</c>, <c>Channel</c>, <c>BotEmail</c>, or <c>ApiKey</c>) is missing or whitespace.
    /// </exception>
    public ZulipBatchedSink(ZulipSinkOptions options, ITextFormatter formatter)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

        if (string.IsNullOrWhiteSpace(options.ServerUrl))
            throw new ArgumentException("ServerUrl is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Channel))
            throw new ArgumentException("Channel is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.BotEmail))
            throw new ArgumentException("BotEmail is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("ApiKey is required", nameof(options));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        var authHeader = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.BotEmail}:{options.ApiKey}"));

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Serilog.Sinks.Zulip");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZulipBatchedSink"/> class using a specific HTTP message handler.
    /// </summary>
    /// <remarks>
    /// This constructor is marked as <c>internal</c> to facilitate unit testing by allowing the injection 
    /// of a mocked <see cref="HttpMessageHandler"/>, preventing actual network calls during test execution.
    /// </remarks>
    /// <param name="options">The configuration options for the Zulip sink, including credentials and routing parameters.</param>
    /// <param name="formatter">The formatter used to render each log event into its final text representation.</param>
    /// <param name="messageHandler">The custom HTTP handler used for network communication (e.g., a Mock handler).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/>, <paramref name="formatter"/>, or <paramref name="messageHandler"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when any required string property on <paramref name="options"/> (<c>ServerUrl</c>, <c>Channel</c>, <c>BotEmail</c>, or <c>ApiKey</c>) is missing, empty, or consists only of white-space characters.
    /// </exception>
    internal ZulipBatchedSink(ZulipSinkOptions options, ITextFormatter formatter, HttpMessageHandler messageHandler)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

        // Ensure the injected handler isn't null before passing it to HttpClient
        if (messageHandler is null)
            throw new ArgumentNullException(nameof(messageHandler));

        if (string.IsNullOrWhiteSpace(options.ServerUrl))
            throw new ArgumentException("ServerUrl is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Channel))
            throw new ArgumentException("Channel is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.BotEmail))
            throw new ArgumentException("BotEmail is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("ApiKey is required", nameof(options));

        // Initialize the HttpClient using the injected handler (Mock or Real)
        _httpClient = new HttpClient(messageHandler)
        {
            BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Set up Basic Authentication for the Zulip API
        var authHeader = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.BotEmail}:{options.ApiKey}"));

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Serilog.Sinks.Zulip");
    }

    /// <summary>
    /// Emits a batch of log events to Zulip.
    /// Events are grouped by topic (either <see cref="ZulipSinkOptions.DefaultTopic"/> or the log level).
    /// </summary>
    /// <param name="batch">The batch of log events to send.</param>
    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        var groupedLogs = batch.GroupBy(log => _options.DefaultTopic ?? log.Level.ToString());

        foreach (var group in groupedLogs)
        {
            await SendGroupToZulipAsync(group.Key, group).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends a group of log events that share the same topic as a single Zulip message.
    /// Each log event is rendered using the configured <see cref="ITextFormatter"/>.
    /// </summary>
    /// <param name="topic">The topic (subject) of the Zulip message.</param>
    /// <param name="logs">The log events to include in the message.</param>
    private async Task SendGroupToZulipAsync(string topic, IEnumerable<LogEvent> logs)
    {
        var contentBuilder = new StringBuilder();

        foreach (var log in logs)
        {
            await using var writer = new StringWriter();
            _formatter.Format(log, writer);
            contentBuilder.AppendLine(writer.ToString());
        }

        var requestParams = new Dictionary<string, string>
        {
            { "type", "channel" },
            { "to", _options.Channel },
            { "topic", topic },
            { "content", contentBuilder.ToString() }
        };

        try
        {
            using var content = new FormUrlEncodedContent(requestParams);

            var response = await _httpClient
                .PostAsync(ApiPath, content)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Serilog.Debugging.SelfLog.WriteLine("Zulip Sink Error: {0} - {1}", response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            Serilog.Debugging.SelfLog.WriteLine("Zulip Sink Exception: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Called when the batching sink has an empty batch (no-op).
    /// </summary>
    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    /// <summary>
    /// Releases the unmanaged resources used by the sink.
    /// </summary>
    public void Dispose() => _httpClient.Dispose();
}