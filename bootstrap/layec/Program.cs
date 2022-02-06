#define NO_DEFAULT_SOURCE_DIRECTORY // if we allow `./` as the default target, we shouldn't print help text on no arguments given. This flag controls wether or not `layec` prints help text when no arguments are passed

using System.Diagnostics;

using laye;
using laye.Backends;
using laye.Backends.Llvm;
using laye.Compiler;

using static CompilerStatus;

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

    Arguments = args;

    string inDir = args.SourcePath;
    bool inDirRecurse = !args.NoRecursiveSourceDirectory;

    string[] sourceFiles = GetSourceFilesInDirectory(inDir, inDirRecurse).ToArray();

    if (inDirRecurse)
        ShowInfo($"Compiling {sourceFiles.Length} .ly files in `{inDir}` recursively");
    else ShowInfo($"Compiling {sourceFiles.Length} .ly files in `{inDir}` at top-level only");

    var diagnostics = new List<Diagnostic>();
    var sourceSyntaxes = new Dictionary<string, LayeAstRoot>();
    var globalSymbols = new SymbolTable();

    var parsingTimer = Stopwatch.StartNew();
    foreach (string sourceFile in sourceFiles)
    {
        ShowVerbose($"Parsing `{sourceFile}`");

        var sourceSyntax = LayeParser.ParseSyntaxFromFile(sourceFile, diagnostics);
        sourceSyntaxes[sourceFile] = new(sourceSyntax);

        if (args.PrintSyntaxTrees)
        {
            var prettyPrinter = new DebugPrettyPrinter(File.ReadAllText(sourceFile));
            prettyPrinter.PrettyPrint(sourceSyntax);
        }
    }

    parsingTimer.Stop();

    if (diagnostics.Any(d => d is Diagnostic.Error))
    {
        ShowInfo("Parsing failed");
        PrintDiagnostics(diagnostics);
        return 1;
    }

    var elapsedParsingTime = parsingTimer.Elapsed;
    ShowInfo($"Parsing took {elapsedParsingTime.TotalSeconds:N2}s");

    var checkingTimer = Stopwatch.StartNew();
    ShowInfo($"Checking program semantics");

    var cstRoots = LayeChecker.CheckSyntax(sourceSyntaxes.Values.ToArray(), globalSymbols, diagnostics);

    checkingTimer.Stop();

    if (cstRoots.Length != sourceSyntaxes.Count || diagnostics.Any(d => d is Diagnostic.Error))
    {
        ShowInfo("Checking failed");
        PrintDiagnostics(diagnostics);
        return 1;
    }
    else
    {
        var elapsedCheckingTime = checkingTimer.Elapsed;
        ShowInfo($"Semantic analysis took {elapsedCheckingTime.TotalSeconds:N2}s");
    }

    IBackend backend = args.Backend switch
    {
        Backend.Llvm => new LlvmBackend(),
        _ => throw new NotImplementedException(),
    };

    var backendOptions = new BackendOptions()
    {
        OutputFileName = args.OutputFileName,
        KeepTemporaryFiles = args.KeepTemporaryFiles,
        ShowBackendOutput = args.ShowBackendOutput,
    };

    int returnCode = backend.Compile(cstRoots, backendOptions);
    if (!args.QuietOutput || args.ShowBackendOutput)
        Console.WriteLine();

    PrintDiagnostics(diagnostics);
    return returnCode;
}

static void PrintDiagnostics(IEnumerable<Diagnostic> diagnostics)
{
    foreach (Diagnostic diagnostic in diagnostics)
    {
        if (diagnostic is Diagnostic.Error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("error: ");
        }
        else if (diagnostic is Diagnostic.Warning)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("warning: ");
        }

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"{diagnostic.SourceSpan}: ");
        Console.ForegroundColor = ConsoleColor.White;
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

    [CommandLine.Option('q', "quiet", Default = false, HelpText = "Disables printing compiler stages and timings to the console.")]
    public bool QuietOutput { get; set; } = false;

    [CommandLine.Option('v', "verbose", Default = false, HelpText = "Enabler printing additional compiler stage messages to the console. This flag is ignored if --quite is provided.")]
    public bool VerboseOutput { get; set; } = false;

    [CommandLine.Option("no-recursive-source", Default = false, HelpText = "Disables recursive directory searching when compiling a source directory.")]
    public bool NoRecursiveSourceDirectory { get; set; } = false;

    [CommandLine.Option("temp-files", Default = false, HelpText = "Keeps temporary files around after compilation.")]
    public bool KeepTemporaryFiles { get; set; } = false;

    [CommandLine.Option("backend-output", Default = false, HelpText = "Shows output from backend processes.")]
    public bool ShowBackendOutput { get; set; } = false;

    [CommandLine.Option("backend", Default = Backend.Llvm, HelpText = "The backend used to generate the output file.")]
    public Backend Backend { get; set; } = Backend.Llvm;

    [CommandLine.Option("help", Default = false, HelpText = "Display this help documentation.")]
    public bool ShowHelp { get; set; } = false;

    [CommandLine.Option("print-syntax", Default = false, HelpText = "Pretty-print syntax trees as they are parsed.")]
    public bool PrintSyntaxTrees { get; set; } = false;
}

internal static class CompilerStatus
{
    public static ProgramArgs Arguments = default!;

    public static void ShowVerbose(string message)
    {
        if (Arguments.QuietOutput) return;
        if (!Arguments.VerboseOutput) return;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("[VERBOSE] ");
        Console.WriteLine(message);
    }

    public static void ShowInfo(string message)
    {
        if (Arguments.QuietOutput) return;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("[INFO] ");
        Console.WriteLine(message);
    }

    public static void ShowCommand(string message)
    {
        if (Arguments.QuietOutput) return;
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("[CMD] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(message);
    }
}
