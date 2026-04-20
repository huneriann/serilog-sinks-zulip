namespace Serilog.Sinks.Zulip.UnitTest;

using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Serilog.Sinks.Zulip;
using Xunit;

public class ZulipSinkUnitTests
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
    public void Constructor_MissingRequiredOptions_ThrowsArgumentException()
    {
        // Arrange
        var invalidOptions = new ZulipSinkOptions
        {
            ServerUrl = "", // Missing
            BotEmail = "bot@test.com",
            ApiKey = "test-key",
            Channel = "test-stream"
        };

        // Act
        Action act = () => new ZulipBatchedSink(invalidOptions, _formatter);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*ServerUrl is required*");
    }

    [Fact]
    public async Task EmitBatchAsync_SendsGroupedLogs_Successfully()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        using var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);

        var batch = new List<LogEvent>
        {
            CreateLogEvent(LogEventLevel.Error, "First error"),
            CreateLogEvent(LogEventLevel.Warning, "First warning")
        };

        // Act
        await sink.EmitBatchAsync(batch);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(), // Grouped into 1 topic, so it should only make 1 HTTP request
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("api/v1/messages")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task EmitBatchAsync_ApiReturnsError_DoesNotThrowException()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized, // Simulating a bad API Key
                Content = new StringContent("Invalid API Key")
            });

        using var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);

        var batch = new List<LogEvent> { CreateLogEvent(LogEventLevel.Error, "Test error") };

        // Act
        Func<Task> act = async () => await sink.EmitBatchAsync(batch);

        // Assert
        await act.Should().NotThrowAsync(); // Serilog sinks must NEVER throw on failure
    }

    [Fact]
    public async Task EmitBatchAsync_NetworkException_DoesNotThrowException()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network is down"));

        using var sink = new ZulipBatchedSink(_validOptions, _formatter, handlerMock.Object);

        var batch = new List<LogEvent> { CreateLogEvent(LogEventLevel.Error, "Test error") };

        // Act
        Func<Task> act = async () => await sink.EmitBatchAsync(batch);

        // Assert
        await act.Should().NotThrowAsync(); // Must swallow network crashes
    }

    // --- Helper Method to generate fake Serilog Logs ---
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