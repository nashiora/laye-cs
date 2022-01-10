using laye.parser;
using laye.syntax.Ast;

namespace laye;

internal sealed class ConsoleDiagnosticSink : IDiagnosticSink
{
    public bool OutputColoring { get; set; } = true;

    private void SetColor(ConsoleColor color)
    {
        if (OutputColoring)
            Console.ForegroundColor = color;
    }

    public void Information(string message, params object[] args)
    {
        Console.WriteLine(string.Format(message, args));
    }

    public void Information(string sourceName, SourceLocation location, string message, params object[] args)
    {
        Information($"{sourceName}{location}: {message}", args);
    }

    public void Warning(string message, params object[] args)
    {
        SetColor(ConsoleColor.DarkMagenta);
        Console.Write("warning: ");
        SetColor(ConsoleColor.White);
        Console.WriteLine(string.Format(message, args));
    }

    public void Warning(string sourceName, SourceLocation location, string message, params object[] args)
    {
        Warning($"{sourceName}{location}: {message}", args);
    }

    public void Error(string message, params object[] args)
    {
        SetColor(ConsoleColor.Red);
        Console.Write("error: ");
        SetColor(ConsoleColor.White);
        Console.WriteLine(string.Format(message, args));
    }

    public void Error(string sourceName, SourceLocation location, string message, params object[] args)
    {
        Error($"{sourceName}{location}: {message}", args);
    }
}
