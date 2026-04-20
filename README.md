# Serilog.Sinks.Zulip

![serilog-sinks-zulip-icon](https://raw.githubusercontent.com/huneriann/serilog-sinks-zulip/main/icon.png?raw=true)

[![NuGet](https://img.shields.io/nuget/v/serilog.sinks.zulip.svg)](https://www.nuget.org/packages/serilog.sinks.zulip)
[![Nuget Downloads](https://img.shields.io/nuget/dt/serilog.sinks.zulip)](https://www.nuget.org/packages/serilog.sinks.zulip)
[![publish to nuget](https://github.com/huneriann/serilog-sinks-zulip/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/huneriann/serilog-sinks-zulip/actions/workflows/ci-cd.yml)

A high-performance, batched asynchronous Serilog sink for the Zulip messaging platform.

## Overview

This library provides a production-grade integration for routing structured logs into Zulip channels. It is designed to be non-blocking and network-efficient, ensuring that logging telemetry does not impact application latency.

For more information on the underlying REST API, please refer to the official [Zulip API Documentation](https://zulip.com/api/).

## Installation

```text
dotnet add package Serilog.Sinks.Zulip
```

## Configuration Options

The sink can be customized using the `ZulipSinkOptions` object (or mapped directly from `appsettings.json`).

| Property | Type | Required | Default | Description |
| :--- | :--- | :---: | :--- | :--- |
| **ServerUrl** | `string` | Yes | - | The base URL of your Zulip server (e.g., `https://your-org.zulipchat.com`). |
| **BotEmail** | `string` | Yes | - | The email address of the bot user sending the logs. |
| **ApiKey** | `string` | Yes | - | The API key generated for the bot user in Zulip. |
| **Channel** | `string` | Yes | `"general"` | The channel (stream) ID or name where logs will be routed. |
| **DefaultTopic** | `string?` | No | *Log Level* | The topic under which messages will be grouped. If left null, logs are grouped by their `LogEventLevel` (e.g., "Error", "Warning"). |

> 💡 **Tip:** In Zulip, topics are a powerful way to thread and organize messages within a channel. To learn more about how they work, check out the official [Introduction to Topics](https://zulip.com/help/introduction-to-topics) guide.

### Advanced Batching & Formatting Options
When configuring via the extension methods, you also have access to standard Serilog batching and formatting controls:

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **restrictedToMinimumLevel** | `LogEventLevel` | `Debug` | The minimum log level required to trigger a Zulip message. |
| **batchSizeLimit** | `int` | `50` | The maximum number of log events sent in a single HTTP request. |
| **periodSeconds** | `int` | `2` | The time to wait between batch transmissions. |
| **outputTemplate** | `string` | *Standard Serilog* | Customize the markdown layout of the message sent to Zulip. |

---

## Usage Examples

### Approach 1: `appsettings.json` (Recommended)

This library fully supports `Serilog.Settings.Configuration`. Using `appsettings.json` prevents hardcoding API keys into your source code.

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Zulip" ],
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Zulip",
        "Args": {
          "serverUrl": "[https://your-org.zulipchat.com](https://your-org.zulipchat.com)",
          "botEmail": "log-bot@your-org.zulipchat.com",
          "apiKey": "YOUR_ZULIP_API_KEY",
          "channel": "backend-alerts",
          "defaultTopic": "Production Errors",
          "restrictedToMinimumLevel": "Warning",
          "outputTemplate": "**[{Level}]** {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

## Approach 2: C# Fluent Configuration
If you prefer to configure Serilog via code, you can pass options directly during startup.

```C#
using Serilog;
using Serilog.Sinks.Zulip;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Zulip(
        serverUrl: "[https://your-org.zulipchat.com](https://your-org.zulipchat.com)",
        botEmail: "log-bot@your-org.zulipchat.com",
        apiKey: "YOUR_ZULIP_API_KEY",
        channel: "backend-alerts",
        defaultTopic: "Production Errors",
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning
    )
    .CreateLogger();
```