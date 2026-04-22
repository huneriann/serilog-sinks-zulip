namespace Serilog.Sinks.Zulip.UnitTest;

public class ZulipBatchedSinkTests
{
    private readonly ZulipSinkOptions _validOptions = new()
    {
        ServerUrl = "https://test.zulipchat.com",
        BotEmail = "bot@test.com",
        ApiKey = "test-key",
        Channel = "test-stream",
        DefaultTopic = "TestTopic"
    };

    private readonly MessageTemplateTextFormatter _formatter = new("{Message}", null);

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Action act = () => new ZulipBatchedSink(null!, _formatter);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_NullFormatter_ThrowsArgumentNullException()
    {
        Action act = () => new ZulipBatchedSink(_validOptions, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("formatter");
    }

    [Fact]
    public void InternalConstructor_NullMessageHandler_ThrowsArgumentNullException()
    {
        Action act = () => new ZulipBatchedSink(_validOptions, _formatter, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("messageHandler");
    }

    [Theory]
    [InlineData("", "channel", "email", "key", "ServerUrl")]
    [InlineData("url", "", "email", "key", "Channel")]
    [InlineData("url", "channel", "", "key", "BotEmail")]
    [InlineData("url", "channel", "email", "", "ApiKey")]
    public void Constructor_MissingRequiredStrings_ThrowsArgumentException(
        string url, string channel, string email, string key, string expectedMissingProperty)
    {
        // Arrange
        var invalidOptions = new ZulipSinkOptions
        {
            ServerUrl = url,
            Channel = channel,
            BotEmail = email,
            ApiKey = key
        };

        // Act & Assert for public constructor
        Action actPublic = () => new ZulipBatchedSink(invalidOptions, _formatter);
        actPublic.Should().Throw<ArgumentException>()
            .WithMessage($"*{expectedMissingProperty}*") // Check that the message mentions the missing property
            .WithParameterName("options"); // Check that the parameter flagged is 'options'

        // Act & Assert for internal constructor
        Action actInternal = () =>
            new ZulipBatchedSink(invalidOptions, _formatter, new Mock<HttpMessageHandler>().Object);
        actInternal.Should().Throw<ArgumentException>()
            .WithMessage($"*{expectedMissingProperty}*")
            .WithParameterName("options");
    }

    [Fact]
    public async Task EmitBatchAsync_SendsGroupedLogs_Successfully()
    {
        var handlerMock = SetupMockHttpMessageHandler(HttpStatusCode.OK);
        using var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);

        var batch = new List<LogEvent>
        {
            CreateLogEvent(LogEventLevel.Error, "First error"),
            CreateLogEvent(LogEventLevel.Warning, "First warning")
        };

        await sink.EmitBatchAsync(batch);

        // Grouped into 1 topic ("TestTopic"), so it should only make 1 HTTP request
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task EmitBatchAsync_NullDefaultTopic_GroupsByLogLevel()
    {
        var optionsWithoutTopic = new ZulipSinkOptions
        {
            ServerUrl = "https://test.zulipchat.com",
            BotEmail = "bot@test.com",
            ApiKey = "test-key",
            Channel = "test-stream",
            DefaultTopic = null // Force fallback to Log Level
        };

        var handlerMock = SetupMockHttpMessageHandler(HttpStatusCode.OK);
        using var sink = new ZulipBatchedSink(optionsWithoutTopic, _formatter, handlerMock.Object);

        var batch = new List<LogEvent>
        {
            CreateLogEvent(LogEventLevel.Error, "Error one"),
            CreateLogEvent(LogEventLevel.Error, "Error two"),
            CreateLogEvent(LogEventLevel.Information, "Info one")
        };

        await sink.EmitBatchAsync(batch);

        // Two distinct log levels (Error and Information), so we expect exactly 2 requests
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task EmitBatchAsync_ApiReturnsError_LogsToSelfLogAndDoesNotThrow()
    {
        // Intercept Serilog's SelfLog output
        using var stringWriter = new StringWriter();
        Serilog.Debugging.SelfLog.Enable(stringWriter);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Invalid API Key")
            });

        using var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);
        var batch = new List<LogEvent> { CreateLogEvent(LogEventLevel.Error, "Test error") };

        // Act
        Func<Task> act = async () => await sink.EmitBatchAsync(batch);

        // Assert
        await act.Should().NotThrowAsync();

        var selfLogOutput = stringWriter.ToString();
        selfLogOutput.Should().Contain("Zulip Sink Error: Unauthorized");
        selfLogOutput.Should().Contain("Invalid API Key");
    }

    [Fact]
    public async Task EmitBatchAsync_NetworkException_LogsToSelfLogAndDoesNotThrow()
    {
        using var stringWriter = new StringWriter();
        Serilog.Debugging.SelfLog.Enable(stringWriter);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network is down"));

        using var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);
        var batch = new List<LogEvent> { CreateLogEvent(LogEventLevel.Error, "Test error") };

        // Act
        Func<Task> act = async () => await sink.EmitBatchAsync(batch);

        // Assert
        await act.Should().NotThrowAsync();

        var selfLogOutput = stringWriter.ToString();
        selfLogOutput.Should().Contain("Zulip Sink Exception: Network is down");
    }

    [Fact]
    public async Task OnEmptyBatchAsync_ReturnsCompletedTask()
    {
        var handlerMock = SetupMockHttpMessageHandler(HttpStatusCode.OK);
        using var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);

        Func<Task> act = async () => await sink.OnEmptyBatchAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_DisposesHttpClient_WithoutThrowing()
    {
        var handlerMock = SetupMockHttpMessageHandler(HttpStatusCode.OK);
        var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);

        Action act = () => sink.Dispose();

        act.Should().NotThrow();
    }

    private static Mock<HttpMessageHandler> SetupMockHttpMessageHandler(HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = statusCode });
        return handlerMock;
    }

    private static LogEvent CreateLogEvent(LogEventLevel level, string message)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            null,
            new MessageTemplate(message, Array.Empty<MessageTemplateToken>()),
            Array.Empty<LogEventProperty>());
    }
}