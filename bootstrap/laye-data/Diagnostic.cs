namespace laye;

public abstract record class Diagnostic(SourceSpan SourceSpan, string Message)
{
    public sealed record class Information(SourceSpan SourceSpan, string Message)
        : Diagnostic(SourceSpan, Message);
    public sealed record class Warning(SourceSpan SourceSpan, string Message)
        : Diagnostic(SourceSpan, Message);
    public sealed record class Error(SourceSpan SourceSpan, string Message)
        : Diagnostic(SourceSpan, Message);
}
