namespace Serilog.Sinks.Zulip.UnitTest;

public class ZulipLoggerConfigurationExtensionsTests
{
    private static LoggerSinkConfiguration GetLoggerSinkConfiguration()
    {
        return new LoggerConfiguration().WriteTo;
    }

    private static ZulipSinkOptions GetValidOptions()
    {
        return new ZulipSinkOptions
        {
            ServerUrl = "https://test.zulipchat.com",
            Channel = "general",
            BotEmail = "test-bot@zulipchat.com",
            ApiKey = "secret-api-key"
        };
    }

    [Fact]
    public void Zulip_FullOverload_NullLoggerSinkConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        LoggerSinkConfiguration? config = null;
        var options = GetValidOptions();
        var batchingOptions = new PeriodicBatchingSinkOptions();
        var formatter = new MessageTemplateTextFormatter("{Message}");

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            config!.Zulip(options, batchingOptions, formatter));

        Assert.Equal("loggerSinkConfiguration", exception.ParamName);
    }

    [Fact]
    public void Zulip_FullOverload_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();
        var batchingOptions = new PeriodicBatchingSinkOptions();
        var formatter = new MessageTemplateTextFormatter("{Message}");

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            config.Zulip(null!, batchingOptions, formatter));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Zulip_FullOverload_NullBatchingOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();
        var options = GetValidOptions();
        var formatter = new MessageTemplateTextFormatter("{Message}");

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            config.Zulip(options, null!, formatter));

        Assert.Equal("batchingOptions", exception.ParamName);
    }

    [Fact]
    public void Zulip_FullOverload_NullFormatter_ThrowsArgumentNullException()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();
        var options = GetValidOptions();
        var batchingOptions = new PeriodicBatchingSinkOptions();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            config.Zulip(options, batchingOptions, null!));

        Assert.Equal("formatter", exception.ParamName);
    }

    [Fact]
    public void Zulip_FullOverload_ValidArguments_ReturnsLoggerConfiguration()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();
        var options = GetValidOptions();
        var batchingOptions = new PeriodicBatchingSinkOptions();
        var formatter = new MessageTemplateTextFormatter("{Message}");

        // Act
        var result = config.Zulip(options, batchingOptions, formatter);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<LoggerConfiguration>(result);
    }

    [Fact]
    public void Zulip_OptionsOverload_NullLoggerSinkConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        LoggerSinkConfiguration? config = null;
        var options = GetValidOptions();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            config!.Zulip(options));

        Assert.Equal("loggerSinkConfiguration", exception.ParamName);
    }

    [Fact]
    public void Zulip_OptionsOverload_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            config.Zulip(null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Zulip_OptionsOverload_NullFormatter_AppliesDefaultFormatterAndReturnsLoggerConfiguration()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();
        var options = GetValidOptions();

        // Act
        var result = config.Zulip(options, formatter: null);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<LoggerConfiguration>(result);
    }

    [Fact]
    public void Zulip_OptionsOverload_WithFormatter_ReturnsLoggerConfiguration()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();
        var options = GetValidOptions();
        var formatter = new MessageTemplateTextFormatter("{Message}");

        // Act
        var result = config.Zulip(options, formatter);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Zulip_StringParamsOverload_ValidArguments_ReturnsLoggerConfiguration()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();

        // Act
        var result = config.Zulip(
            serverUrl: "https://test.zulipchat.com",
            channel: "general",
            botEmail: "test-bot@zulipchat.com",
            apiKey: "secret-api-key",
            defaultTopic: "AppLogs",
            outputTemplate: "{Message}",
            formatProvider: null,
            restrictedToMinimumLevel: LogEventLevel.Warning,
            batchSizeLimit: 100,
            periodSeconds: 5);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<LoggerConfiguration>(result);
    }

    [Fact]
    public void Zulip_StringParamsOverload_NullOutputTemplate_AppliesDefaultTemplateAndReturnsLoggerConfiguration()
    {
        // Arrange
        var config = GetLoggerSinkConfiguration();

        // Act
        var result = config.Zulip(
            serverUrl: "https://test.zulipchat.com",
            channel: "general",
            botEmail: "test-bot@zulipchat.com",
            apiKey: "secret-api-key",
            outputTemplate: null); // Triggers the `outputTemplate ?? DefaultOutputTemplate` fallback

        // Assert
        Assert.NotNull(result);
    }
}