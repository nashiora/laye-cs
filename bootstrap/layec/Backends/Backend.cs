namespace laye.Backends;

[Flags]
internal enum Backend
{
    None = 0,

    C,
    Llvm,
}

internal interface IBackend
{
    int Compile(LayeCstRoot[] roots, BackendOptions options);
}

internal sealed class BackendOptions
{
    public string OutputFileName { get; set; } = "./output.exe";
    public Version Version { get; set; } = new Version(0, 0, 1, 0);

    public bool KeepTemporaryFiles { get; set; } = false;
    public bool IsExecutable { get; set; } = true;

    public string TargetTriple { get; set; } = "x86_64-pc-windows-msvc";
    public string TargetCpuString { get; set; } = "";
    public string TargetFeaturesString { get; set; } = "";

    public bool ShowBackendOutput { get; set; } = false;

    public string[] AdditionalArguments { get; set; } = Array.Empty<string>();
    public string[] FilesToLinkAgainst { get; set; } = Array.Empty<string>();
}
