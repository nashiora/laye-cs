using CommandLine.Text;

using laye;

bool isVerboseFlagGiven = Array.IndexOf(args, "--verbose") >= 0;
if (isVerboseFlagGiven)
{
    Console.WriteLine("Verbose output enabled");
    Console.WriteLine();
}

var loggerConfig = new LoggerConfiguration()
    .WriteTo.CompilerConsole();

if (isVerboseFlagGiven)
    loggerConfig.MinimumLevel.Verbose();

Log.Logger = loggerConfig.CreateLogger();

var cmdParser = new Parser(p => p.HelpWriter = null);
var cmdParseResult = cmdParser.ParseArguments<NewProjectArguments, BuildProjectArguments, GraphProjectArguments>(args);

cmdParseResult.MapResult<NewProjectArguments, BuildProjectArguments, GraphProjectArguments, int>(
    NewProjectCommand.Entry, BuildProjectCommand.Entry, GraphProjectCommand.Entry, OnError);

int OnError(IEnumerable<Error> errors)
{
    var asmVersion = typeof(Program).Assembly.GetName().Version;
    var asmSemver = new SemVersion(asmVersion);

    var helpText = HelpText.AutoBuild(cmdParseResult, h =>
    {
        h.AdditionalNewLineAfterOption = false;
        h.Heading = $"Laye Build System version {asmSemver}";
        h.Copyright = "Copyright (c) 2021 Local Atticus (nashiora.com)";

        return h;
    }, e => e);

    Console.WriteLine(helpText);
    return 0;
}
