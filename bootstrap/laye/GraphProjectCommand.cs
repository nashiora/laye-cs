using laye.parser;

namespace laye;

[Verb("graph", HelpText = "Show the build dependency graph for a Laye project")]
internal sealed class GraphProjectArguments
{
    [Option("manifest-path", HelpText = "Path to laye.toml")]
    public string ManifestFilePath { get; set; } = ".\\laye.toml";

    [Option("verbose", HelpText = "When set, displays verbose output about the compiler process")]
    public bool VerboseOutput { get; set; }
}

internal static class GraphProjectCommand
{
    public static int Entry(GraphProjectArguments args)
    {
        var getGraphResult = ProjectManager.GenerateProjectBuildGraph(new(args.ManifestFilePath), out var graph);
        if (getGraphResult != ProjectGraphResult.Success)
            return (int)getGraphResult;

        var getNodesResult = graph.GetNodesInDependencyOrder(out var nodes);
        if (getNodesResult.Result != DependencyResolutionResult.Succees)
        {
            Console.WriteLine(getNodesResult.Result);
            return -1;
        }

        foreach (var node in nodes)
            Console.WriteLine(node.NodeName);

        return 0;
    }
}
