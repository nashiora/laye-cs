namespace laye;

[Verb("build", HelpText = "Build a Laye project")]
internal sealed class BuildProjectArguments
{
    [Option("manifest-path", HelpText = "Path to laye.toml")]
    public string ManifestFilePath { get; set; } = ".\\laye.toml";

    [Option("color", HelpText = "Coloring: auto, always, never")]
    public string ConsoleOutputColoring { get; set; } = "always";

    [Option("print-ast", HelpText = "Writes the AST for each file to the console")]
    public bool PrintAstRoots { get; set; }

    [Option("backend", HelpText = "Controls which process is used for code generation [possible values: c]")]
    public string Backend { get; set; } = "c";

    [Option("clang-output", HelpText = "When using the LLVM backend, shows the output of the clang process")]
    public bool ShowClangOutput { get; set; }

    [Option("verbose", HelpText = "When set, displays verbose output about the compiler process")]
    public bool VerboseOutput { get; set; }
}

internal static class BuildProjectCommand
{
    public static int Entry(BuildProjectArguments args)
    {
        return (int)ProjectManager.BuildProject(new(args.ManifestFilePath)
        {
            ConsoleOutputColoring = args.ConsoleOutputColoring,
            PrintAstRoots = args.PrintAstRoots,
            Backend = args.Backend,
        });
    }
}
