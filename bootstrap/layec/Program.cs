#define NO_DEFAULT_SOURCE_DIRECTORY // if we allow `./` as the default target, we shouldn't print help text on no arguments given. This flag controls wether or not `layec` prints help text when no arguments are passed

using laye;
using laye.Backends;
using laye.Backends.Llvm;
using laye.Backends.Msil;
using laye.Compiler;

var cmdParser = new CommandLine.Parser(config =>
{
    config.AutoHelp = false;
});

var parserResult = cmdParser.ParseArguments<ProgramArgs>(args);
#if NO_DEFAULT_SOURCE_DIRECTORY
if (args.Length == 0)
{
    Console.WriteLine(GetHelpText(parserResult));
    return 0;
}
#endif

return CommandLine.ParserResultExtensions.MapResult(parserResult, pa => ProgramEntry(parserResult, pa), errors => ParseError(parserResult, errors));

static int ProgramEntry(CommandLine.ParserResult<ProgramArgs> result, ProgramArgs args)
{
    if (args.ShowHelp)
    {
        Console.WriteLine(GetHelpText(result));
        return 0;
    }

    string inDir = args.SourcePath;
    bool inDirRecurse = !args.NoRecursiveSourceDirectory;

    string[] sourceFiles = GetSourceFilesInDirectory(inDir, inDirRecurse).ToArray();
    // Console.WriteLine(string.Join(Environment.NewLine, sourceFiles));

    var diagnostics = new List<Diagnostic>();
    var sourceSyntaxes = new Dictionary<string, LayeAstRoot>();
    var globalSymbols = new SymbolTable();

    foreach (string sourceFile in sourceFiles)
    {
        var sourceSyntax = LayeParser.ParseSyntaxFromFile(sourceFile, diagnostics);
        sourceSyntaxes[sourceFile] = new(sourceSyntax);

        if (args.PrintSyntaxTrees)
        {
            var prettyPrinter = new DebugPrettyPrinter(File.ReadAllText(sourceFile));
            prettyPrinter.PrettyPrint(sourceSyntax);
        }
    }

    if (diagnostics.Any(d => d is Diagnostic.Error))
    {
        PrintDiagnostics(diagnostics);
        return 1;
    }

    var irModule = LayeChecker.CheckSyntax(sourceSyntaxes.Values.ToArray(), globalSymbols, diagnostics);

    if (diagnostics.Any(d => d is Diagnostic.Error))
    {
        PrintDiagnostics(diagnostics);
        return 1;
    }

    IBackend backend;
    switch (args.Backend)
    {
        case Backend.Msil: backend = new MsilBackend(); break;
        case Backend.Llvm: backend = new LlvmBackend(); break;
        default: throw new NotImplementedException();
    }

    var backendOptions = new BackendOptions()
    {
        OutputFileName = args.OutputFileName,
        KeepTemporaryFiles = args.KeepTemporaryFiles,
    };

    backend.Compile(new[] { irModule }, backendOptions);

    if (diagnostics.Any(d => d is Diagnostic.Error))
    {
        PrintDiagnostics(diagnostics);
        return 1;
    }

    PrintDiagnostics(diagnostics);
    return 0;
}

static void PrintDiagnostics(IEnumerable<Diagnostic> diagnostics)
{
    foreach (Diagnostic diagnostic in diagnostics)
    {
        Console.Write($"{diagnostic.SourceSpan}: ");
        if (diagnostic is Diagnostic.Error)
            Console.Write("error: ");
        Console.WriteLine(diagnostic.Message);
    }
}

static int ParseError(CommandLine.ParserResult<ProgramArgs> result, IEnumerable<CommandLine.Error> errors)
{
    Console.Error.WriteLine(GetHelpText(result));
    return 1;
}

static string GetHelpText(CommandLine.ParserResult<ProgramArgs> result)
{
    const string Heading = "Laye Stand-alone compiler"; // Version + Architecture
    const string Version = "Version 0.1.0";

    return new CommandLine.Text.HelpText(Heading, Version)
    {
        AutoHelp = false,
        AutoVersion = false,
        AddDashesToOption = true,
        AddEnumValuesToHelpText = true,
        AdditionalNewLineAfterOption = false,
        AddNewLineBetweenHelpSections = true,
    }.AddOptions(result);
}

static IEnumerable<string> GetSourceFilesInDirectory(string directoryPath, bool recurse) =>
    Directory.EnumerateFiles(directoryPath, "*.ly", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

sealed class ProgramArgs
{
    [CommandLine.Value(0, MetaName = "source-path",
#if !NO_DEFAULT_SOURCE_DIRECTORY
        Default = "./",
#endif
        HelpText = "A path to either a file or a directory. If a file is given, just that file is compiled. If a directory is given, all source files within that directory are compiled into the same output program. Use the --no-recursive-source flag to only use the top level of the provided directory.")]
    public string SourcePath { get; set; } = "./";

    [CommandLine.Option("il", Default = false, HelpText = "Compile Laye IL files instead of source files.")]
    public bool CompileIL { get; set; } = false;

    [CommandLine.Option('o', "output", Default = "./output.exe", HelpText = "The output file path.")]
    public string OutputFileName { get; set; } = "./output.exe";

    [CommandLine.Option("no-recursive-source", Default = false, HelpText = "Disables recursive directory searching when compiling a source directory.")]
    public bool NoRecursiveSourceDirectory { get; set; } = false;

    [CommandLine.Option("temp-files", Default = false, HelpText = "Keeps temporary files around after compilation.")]
    public bool KeepTemporaryFiles { get; set; } = false;

    [CommandLine.Option("backend", Default = Backend.Llvm, HelpText = "The backend used to generate the output file.")]
    public Backend Backend { get; set; } = Backend.Llvm;

    [CommandLine.Option("help", Default = false, HelpText = "Display this help documentation.")]
    public bool ShowHelp { get; set; } = false;

    [CommandLine.Option("print-syntax", Default = false, HelpText = "Pretty-print syntax trees as they are parsed.")]
    public bool PrintSyntaxTrees { get; set; } = false;
}
