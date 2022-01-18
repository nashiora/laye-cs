#define NO_DEFAULT_SOURCE_DIRECTORY // if we allow `./` as the default target, we shouldn't print help text on no arguments given. This flag controls wether or not `layec` prints help text when no arguments are passed

using laye;
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
    var sourceSyntaxes = new Dictionary<string, LayeAst[]>();
    var globalSymbols = new SymbolTable();

    foreach (string sourceFile in sourceFiles)
    {
        var sourceSyntax = LayeParser.ParseSyntaxFromFile(sourceFile, diagnostics);
        sourceSyntaxes[sourceFile] = sourceSyntax;
    }

    return 0;
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

    [CommandLine.Option('o', "output", Default = "./output.exe", HelpText = "The output file path.")]
    public string OutputFilePath { get; set; } = "./output.exe";

    [CommandLine.Option("no-recursive-source", Default = false, HelpText = "Disables recursive directory searching when compiling a source directory.")]
    public bool NoRecursiveSourceDirectory { get; set; } = false;

    [CommandLine.Option("temp-files", Default = false, HelpText = "Keeps temporary files around after compilation.")]
    public bool KeepTemporaryFiles { get; set; } = false;

    [CommandLine.Option("help", Default = false, HelpText = "Display this help documentation.")]
    public bool ShowHelp { get; set; } = false;
}
