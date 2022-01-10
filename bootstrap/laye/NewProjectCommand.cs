namespace laye;

[Verb("new", HelpText = "Create a new Laye project")]
internal sealed class NewProjectArguments
{
    [Value(0, Required = true)]
    public string Path { get; set; } = null!;

    [Option("exe", HelpText = "Use a executable template [default]")]
    public bool IsExecutable { get; set; }

    [Option("lib", HelpText = "Use a library template")]
    public bool IsLibrary { get; set; }

    [Option("name", HelpText = "Set the resulting project name, defaults to the directory name")]
    public string ProjectName { get; set; } = "";

    [Option("vcs", HelpText = "Initialize a new repository for the given version control system (git) or do not initialize any version control (none) [possible values: git, none]")]
    public string VersionControlSystem { get; set; } = "git";
}

internal static class NewProjectCommand
{
    public static int Entry(NewProjectArguments args)
    {
        if (!Directory.Exists(args.Path))
            Directory.CreateDirectory(args.Path);

        string projectName = args.ProjectName;
        if (string.IsNullOrWhiteSpace(projectName))
            projectName = Path.GetFileNameWithoutExtension(Path.GetFullPath(args.Path));

        return (int)ProjectManager.CreateProjectInDirectory(new(args.Path, projectName)
        {
            IsExecutable = args.IsExecutable,
            IsLibrary = args.IsLibrary,
            VersionControlSystem = args.VersionControlSystem,
        });
    }
}
