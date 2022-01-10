namespace laye;

public sealed class SourceTextCollection
{
    private readonly Dictionary<string, string> m_sources = new();

    public void AddSourceText(string sourceName, string sourceText) => m_sources[sourceName] = sourceText;

    public string ToString(SourceSpan sourceSpan)
    {
        if (!m_sources.TryGetValue(sourceSpan.SourceName, out string? result))
            return "";

        return result!.Substring((int)sourceSpan.StartLocation.SourceIndex, (int)(sourceSpan.EndLocation.SourceIndex - sourceSpan.StartLocation.SourceIndex));
    }
}
