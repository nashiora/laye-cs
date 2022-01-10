using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace laye;

internal sealed class SerilogConsoleSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        switch (logEvent.Level)
        {
            case LogEventLevel.Verbose:
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("verbose");
            } break;

            case LogEventLevel.Error:
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("error");
            } break;

            case LogEventLevel.Information:
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("info");
            } break;
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.WriteLine(logEvent.RenderMessage(null));
    }
}

public static class SerilogExt
{
    public static LoggerConfiguration CompilerConsole(this LoggerSinkConfiguration loggerConfiguration) =>
        loggerConfiguration.Sink(new SerilogConsoleSink());
}
