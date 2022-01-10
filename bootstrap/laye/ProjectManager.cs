using System.Diagnostics;

using laye.parser;

using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace laye;

internal enum ProjectCreateResult
{
    Success = 0,
    InvalidCommandArguments,
    FailedToInitializeGitRepository,
}

internal sealed class ProjectCreateInfo
{
    public string ProjectDirectory { get; }
    public string ProjectName { get; }

    public bool IsExecutable { get; set; } = true;
    public bool IsLibrary { get; set; } = false;
    public string VersionControlSystem { get; set; } = "git";

    public ProjectCreateInfo(string projectDirectory, string projectName)
    {
        ProjectDirectory = projectDirectory;
        ProjectName = projectName;
    }
}

internal enum ProjectGraphResult
{
    Success = 0,
    FailedToGenerateBuildGraph = 1,
    CircularDependency = 2,
}

internal sealed class ProjectGraphInfo
{
    public string ManifestFilePath { get; }

    public ProjectGraphInfo(string manifestFilePath)
    {
        ManifestFilePath = manifestFilePath;
    }
}

internal enum ProjectBuildResult
{
    Success = 0,
    FailedToGenerateBuildGraph,
}

internal sealed class ProjectBuildInfo
{
    public string ManifestFilePath { get; }

    /// <summary>
    /// If this path is relative, it's relative to the manifest directory.
    /// </summary>
    public string OutputDirectory { get; set; } = "./bin";
    /// <summary>
    /// If this path is relative, it's relative to the manifest directory.
    /// </summary>
    public string IntermediateDirectory { get; set; } = "./obj";

    public string ConsoleOutputColoring { get; set; } = "always";
    public bool PrintAstRoots { get; set; } = false;
    public string Backend { get; set; } = "c";
    public bool ShowClangOutput { get; set; } = false;

    public ProjectBuildInfo(string manifestFilePath)
    {
        ManifestFilePath = manifestFilePath;
    }
}

internal static class ProjectManager
{
    const string ExecutableMainFileContents = @"foreign cdecl i32 printf(u8 const [*]format, varargs);

i32 main()
{
    printf(""Hello, hunter!\n"");
    return 0;
}
";

    const string LibraryMainFileContents = @"void test_reminder()
{
}
";

    static void WriteErrorMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("error");

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.WriteLine(message);
    }

    internal static ManifestInfo? GetManifestInfo(string manifestFilePath)
    {
        DocumentSyntax manifestDocument;
        try
        {
            manifestDocument = Toml.Parse(File.ReadAllText(manifestFilePath), manifestFilePath);
        }
        catch
        {
            return null;
        }

        var manifestModel = manifestDocument.ToModel();

        if (manifestModel.TryGetToml("workspace", out var workspaceToml) && workspaceToml is TomlTable workspaceModel)
            return GetWorkspaceManifestInfo(workspaceModel);
        else if (manifestModel.TryGetToml("project", out var projectToml) && projectToml is TomlTable projectModel)
            return GetProjectManifestInfo(projectModel);
        else
        {
            Log.Error("Manifest file {ManifestFilePath} is not a laye workspace or project", manifestFilePath);
            return null;
        }

        bool TryGetValueWithWarnings<TValue>(TomlTable table, string key, out TValue value)
        {
            value = default!;

            if (table.TryGetValue(key, out object? valueObject))
            {
                if (valueObject is TValue checkValue)
                {
                    value = checkValue;
                    return true;
                }
                else if (Convert.ChangeType(valueObject, typeof(TValue)) is TValue convertValue)
                {
                    value = convertValue;
                    return true;
                }
                else
                {
                    Log.Warning("Value {ValueName} exists but could not be converted to type {ValueType}", key, typeof(TValue).Name);
                    return false;
                }
            }

            return false;
        }

        WorkspaceInfo? GetWorkspaceManifestInfo(TomlTable projectModel)
        {
            string projectName = (string)projectModel["name"];
            return new(Path.GetFullPath(manifestFilePath), projectName)
            {
            };
        }

        ProjectInfo? GetProjectManifestInfo(TomlTable projectModel)
        {
            string projectName = (string)projectModel["name"];

            bool nostdlib = false;

            // ========== build ==========
            if (manifestModel.TryGetToml("build", out var buildToml) && buildToml is TomlTable buildModel)
            {
                if (TryGetValueWithWarnings(buildModel, "nostdlib", out bool nostdlibValue))
                    nostdlib = nostdlibValue;
            }

            // ========== dependencies ==========
            List<ProjectDependencyInfo> dependencyInfos;
            if (manifestModel.TryGetToml("dependencies", out var dependenciesToml) && dependenciesToml is TomlTable dependenciesModel)
            {
                var infos = GetDependencyInfos(dependenciesModel);
                if (infos is null)
                    return null;

                dependencyInfos = infos;
            }
            else dependencyInfos = new();

            // ========== dev-dependencies ==========
            List<ProjectDependencyInfo> devDependencyInfos;
            if (manifestModel.TryGetToml("dev-dependencies", out var devDependenciesToml) && devDependenciesToml is TomlTable devDependenciesModel)
            {
                var infos = GetDependencyInfos(devDependenciesModel);
                if (infos is null)
                    return null;

                devDependencyInfos = infos;
            }
            else devDependencyInfos = new();

            return new(Path.GetFullPath(manifestFilePath), projectName)
            {
                NoStdLib = nostdlib,
                Dependencies = dependencyInfos.ToArray(),
                DevelopmentDependencies = devDependencyInfos.ToArray(),
            };
        }

        List<ProjectDependencyInfo>? GetDependencyInfos(TomlTable dependenciesModel)
        {
            var resultInfos = new List<ProjectDependencyInfo>();
            foreach (var (dependencyName, dependencyInfo) in dependenciesModel)
            {
                switch (dependencyInfo)
                {
                    case string versionRequirementString:
                    {
                        if (SemVersionRequirement.TryParse(dependencyName) is not SemVersionRequirement versionRequirement)
                        {
                            Log.Error("{VersionString} is not a valid semver or requirement string", versionRequirementString);
                            return null;
                        }

                        resultInfos.Add(new RegistryDependencyInfo(dependencyName, versionRequirement));
                    } break;

                    case TomlTable dependencyTable:
                    {
                        SemVersionRequirement? versionRequirement = null;
                        if (TryGetValueWithWarnings(dependencyTable, "version", out string versionRequirementString))
                        {
                            if (SemVersionRequirement.TryParse(dependencyName) is not SemVersionRequirement versionRequirementCheck)
                            {
                                Log.Error("{VersionString} is not a valid semver or requirement string", versionRequirementString);
                                return null;
                            }

                            versionRequirement = versionRequirementCheck;
                        }

                        bool isPath = TryGetValueWithWarnings(dependencyTable, "path", out string pathString);
                        bool isGit = TryGetValueWithWarnings(dependencyTable, "git", out string gitUrlString);

                        if (isPath && isGit)
                        {
                            Log.Error("Dependency cannot be both a `path` and a `git` dependency");
                            return null;
                        }

                        bool isRegistry = !isPath && !isGit;

                        if (isRegistry)
                        {
                            if (versionRequirement is null)
                                versionRequirement = new(new(0, 0, 0), new(int.MaxValue, int.MaxValue, int.MaxValue));

                            resultInfos.Add(new RegistryDependencyInfo(dependencyName, versionRequirement));
                        }
                        else if (isPath)
                        {
                            resultInfos.Add(new LocalDependencyInfo(dependencyName, pathString)
                            {
                                RegistryVersion = versionRequirement,
                            });
                        }
                        else if (isGit)
                        {
                            string? branch = null;
                            if (TryGetValueWithWarnings(dependencyTable, "branch", out string branchValue))
                                branch = branchValue;

                            resultInfos.Add(new GitDependencyInfo(dependencyName, gitUrlString)
                            {
                                Branch = branch,
                            });
                        }
                    } break;

                    default:
                    {
                        Log.Warning("Unable to make sense of dependency info {DependencyInfoName}", dependencyName);
                    } break;
                }
            }

            return resultInfos;
        }
    }

    public static ProjectCreateResult CreateProjectInDirectory(ProjectCreateInfo args)
    {
        string projectPath = args.ProjectDirectory;
        string projectName = args.ProjectName;

        string sourcePath = Path.Combine(projectPath, "src");
        if (!Directory.Exists(sourcePath))
            Directory.CreateDirectory(sourcePath);

        if (args.IsExecutable && args.IsLibrary)
        {
            WriteErrorMessage("can't specify both library and executable types");
            return ProjectCreateResult.InvalidCommandArguments;
        }

        bool isExecutable = !args.IsLibrary;
        if (isExecutable)
        {
            string mainFilePath = Path.Combine(sourcePath, "main.ly");
            File.WriteAllText(mainFilePath, ExecutableMainFileContents);
        }
        else
        {
            string libFilePath = Path.Combine(sourcePath, "lib.ly");
            File.WriteAllText(libFilePath, LibraryMainFileContents);
        }

        string projectFilePath = Path.Combine(projectPath, "laye.toml");
        var projectDocument = new DocumentSyntax()
        {
            Tables =
            {
                new TableSyntax("project")
                {
                    Items =
                    {
                        { "name", projectName },
                        { "version", "0.1.0" },
                        { "output", isExecutable ? "executable" : "library" },
                    }
                },

                new TableSyntax("dependencies"),
            }
        };

        string projectDocumentText = projectDocument.ToString();
        File.WriteAllText(projectFilePath, projectDocumentText);

        switch (args.VersionControlSystem)
        {
            case "git":
            {
                string gitignorePath = Path.Combine(projectPath, ".gitignore");
                File.WriteAllText(gitignorePath, "build/");

                var gitStartInfo = new ProcessStartInfo()
                {
                    FileName = "git",
                    Arguments = "init",
                    WorkingDirectory = projectPath,
                };

                var gitProcess = Process.Start(gitStartInfo);
                if (gitProcess is null)
                {
                    WriteErrorMessage("failed to start git process");
                    return ProjectCreateResult.FailedToInitializeGitRepository;
                }

                gitProcess.WaitForExit();
            } break;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("   Created");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" {(isExecutable ? "executable" : "library")} `{projectName}` project");

        return ProjectCreateResult.Success;
    }

    public static ProjectGraphResult GenerateProjectBuildGraph(ProjectGraphInfo args, out DependencyGraph<ProjectInfo> buildGraph)
    {
        buildGraph = new();
        var graph = buildGraph;

        var resolvedManifestFiles = new List<ProjectDependencyInfo>();
        var seenManifestFiles = new List<ProjectDependencyInfo>();

        var rootManifestInfo = GetManifestInfo(args.ManifestFilePath);
        if (rootManifestInfo is ProjectInfo rootProjectInfo)
            return Populate(rootProjectInfo);
        else if (rootManifestInfo is WorkspaceInfo /*rootWorkspaceInfo*/)
        {
            return ProjectGraphResult.FailedToGenerateBuildGraph;
        }
        else return ProjectGraphResult.FailedToGenerateBuildGraph;

        ProjectGraphResult Populate(ProjectInfo projectInfo)
        {
            foreach (var dependency in projectInfo.Dependencies)
            {
                if (!resolvedManifestFiles.Contains(dependency))
                {
                    if (seenManifestFiles.Contains(dependency))
                    {
                        Log.Information("Circular project reference: {ProjectInfoName} -> {DependencyName}", projectInfo.Name, dependency.Name);
                        return ProjectGraphResult.CircularDependency;
                    }
                    else
                    {
                        switch (dependency)
                        {
                            case LocalDependencyInfo localDependencyInfo:
                            {
                                var localManifestInfo = GetManifestInfo(localDependencyInfo.Path);
                                if (localManifestInfo is not ProjectInfo localProjectInfo)
                                {
                                    Log.Information("Referenced local dependency {DependencyName} (`{DependencyPath}`) was not a project", localDependencyInfo.Name, localDependencyInfo.Path);
                                    return ProjectGraphResult.FailedToGenerateBuildGraph;
                                }

                                var dependencyResult = Populate(localProjectInfo);
                                if (dependencyResult != ProjectGraphResult.Success)
                                    return dependencyResult;
                            } break;

                            default:
                                Log.Warning("unsupported dependency {Dependency}", dependency);
                                return ProjectGraphResult.FailedToGenerateBuildGraph;
                        }
                    }
                }
            }

            var projectNode = new DependencyGraphNode<ProjectInfo>(projectInfo.Name, projectInfo);
            graph.AddNode(projectNode);

            return ProjectGraphResult.Success;
        }
    }

    public static ProjectBuildResult BuildProject(ProjectBuildInfo args)
    {
        string manifestDir = Directory.GetParent(args.ManifestFilePath)!.FullName;

        var getGraphResult = GenerateProjectBuildGraph(new(args.ManifestFilePath), out var buildGraph);
        if (getGraphResult != 0)
            return ProjectBuildResult.FailedToGenerateBuildGraph;

        string outDir;
        if (Path.IsPathRooted(args.OutputDirectory))
            outDir = args.OutputDirectory;
        else outDir = Path.Combine(manifestDir, args.OutputDirectory);

        string intermediateDir;
        if (Path.IsPathRooted(args.IntermediateDirectory))
            intermediateDir = args.IntermediateDirectory;
        else intermediateDir = Path.Combine(manifestDir, args.IntermediateDirectory);

        return ProjectBuildResult.Success;
    }

    internal abstract record class ManifestInfo(string ManifestFilePath, string Name);

    internal sealed record class WorkspaceInfo(string ManifestFilePath, string Name) : ManifestInfo(ManifestFilePath, Name);

    internal sealed record class ProjectInfo(string ManifestFilePath, string Name) : ManifestInfo(ManifestFilePath, Name)
    {
        public ProjectDependencyInfo[] Dependencies { get; init; } = Array.Empty<ProjectDependencyInfo>();
        public ProjectDependencyInfo[] DevelopmentDependencies { get; init; } = Array.Empty<ProjectDependencyInfo>();

        public bool NoStdLib { get; init; }
    }

    internal abstract record class ProjectDependencyInfo(string Name) { }

    internal sealed record class RegistryDependencyInfo(string Name, SemVersionRequirement Version) : ProjectDependencyInfo(Name)
    {
        public string? RegistryName { get; init; }
    }

    internal sealed record class GitDependencyInfo(string Name, string Url) : ProjectDependencyInfo(Name)
    {
        public string? Branch { get; init; }
    }

    internal sealed record class LocalDependencyInfo(string Name, string Path) : ProjectDependencyInfo(Name)
    {
        /// <summary>
        /// If the project is to be put on a registry, what version of the dependency to rely on instead of the local version.
        /// </summary>
        public SemVersionRequirement? RegistryVersion { get; init; }
    }
}
