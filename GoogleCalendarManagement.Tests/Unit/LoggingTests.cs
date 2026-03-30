using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using SerilogTimings.Extensions;
using System.Text;

namespace GoogleCalendarManagement.Tests.Unit;

/// <summary>
/// Simple in-memory sink for capturing Serilog output in tests (Serilog 4.x removed WriteTo.TextWriter from core).
/// </summary>
internal sealed class InMemorySink : ILogEventSink
{
    private readonly MessageTemplateTextFormatter _formatter;
    private readonly StringBuilder _output = new();

    public InMemorySink(string outputTemplate = "[{Level:u3}] {Message:lj}{NewLine}")
    {
        _formatter = new MessageTemplateTextFormatter(outputTemplate);
    }

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        _output.Append(writer.ToString());
    }

    public string Output => _output.ToString();
}

public class LoggingTests : IDisposable
{
    private readonly string _testLogDir = Path.Combine(Path.GetTempPath(), $"gcm-log-test-{Guid.NewGuid()}");

    private void ConfigureSerilogToFile(string logPath)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    [Fact]
    public void Logger_ShouldWrite_ToFile()
    {
        ConfigureSerilogToFile(Path.Combine(_testLogDir, "test-.txt"));
        Log.Information("Test log entry");
        Log.CloseAndFlush();

        var logFiles = Directory.GetFiles(_testLogDir, "test-*.txt");
        logFiles.Should().NotBeEmpty();
        File.ReadAllText(logFiles[0]).Should().Contain("Test log entry");
    }

    [Fact]
    public void Logger_ShouldInclude_Enrichment()
    {
        var sink = new InMemorySink("{Message} {Properties}{NewLine}");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("Application", "GoogleCalendarManagement")
            .WriteTo.Sink(sink)
            .CreateLogger();

        Log.Information("Test enrichment");
        Log.CloseAndFlush();

        sink.Output.Should().Contain("Application");
    }

    [Fact]
    public void Logger_ShouldTrack_SlowOperations()
    {
        var sink = new InMemorySink("[{Level:u3}] {Message:lj}{NewLine}");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // OperationAt with warnIfExceeds elevates to Warning when threshold exceeded
        using (Log.Logger.OperationAt(LogEventLevel.Information, null, TimeSpan.FromSeconds(1))
                         .Time("Slow operation"))
        {
            Thread.Sleep(1100);
        }
        Log.CloseAndFlush();

        var output = sink.Output;
        output.Should().Contain("Slow operation");
        output.Should().Contain("[WRN]");
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
        if (Directory.Exists(_testLogDir))
            Directory.Delete(_testLogDir, recursive: true);
    }
}
