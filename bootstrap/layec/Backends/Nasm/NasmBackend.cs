namespace laye.Backends.Fasm;

internal sealed class NasmBackend : IBackend
{
    private readonly Dictionary<string, int> m_stringLiterals = new();

    public void Compile(LayeIrModule[] modules, BackendOptions options)
    {
    }

    private void HandleString(LayeIr.String stringValue)
    {
        int literalIndex;
    }
}
