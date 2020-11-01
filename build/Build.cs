using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]

class Build : NukeBuild
{
	public static int Main() => Execute<Build>(x => x.Compile);

	[Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
	readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

	[Parameter("Nuget Version Prefix")]
	readonly string NugetVersionPrefix = "0.0.1";

	[Parameter("Nuget Version Suffix")]
	readonly string NugetVersionSuffix = String.Empty;

	[Parameter]
	string NugetApiUrl = "https://api.nuget.org/v3/index.json"; //default

	[Parameter]
	string NugetApiKey;

	[Solution] readonly Solution Solution;
	[GitRepository] readonly GitRepository GitRepository;

	AbsolutePath SourceDirectory => RootDirectory / "src";
	AbsolutePath TestsDirectory => RootDirectory / "tests";
	AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

	Target Clean => _ => _
			.Before(Restore)
			.Executes(() =>
			{
				SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
				TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
				EnsureCleanDirectory(ArtifactsDirectory);
			});

	Target Restore => _ => _
			.Executes(() =>
			{
				DotNetRestore(s => s
					.AddSources("https://www.myget.org/F/phyros/api/v3/index.json")
					.SetProjectFile(Solution));
			});

	Target Compile => _ => _
			.DependsOn(Restore)
			.Executes(() =>
			{
				DotNetBuild(s => s
							.SetProjectFile(Solution)
							.SetConfiguration(Configuration)
							.EnableNoRestore());
			});

	Target Pack => _ => _
		.DependsOn(Compile)
		.Produces(ArtifactsDirectory / "*.nupkg")
		.Executes(() =>
		{
			DotNetPack(s => s
				.SetProject(Solution.GetProject(BuildSettings.ProjectName))
				.SetConfiguration(Configuration)
				.EnableNoBuild()
				.EnableNoRestore()
				.SetDescription(BuildSettings.Description)
				.SetPackageTags(BuildSettings.PackageTags)
				.SetNoDependencies(true)
				.SetOutputDirectory(ArtifactsDirectory)
				.SetVersionPrefix(NugetVersionPrefix)
				.SetVersionSuffix(NugetVersionSuffix)
				);
		});

	Target Push => _ => _
		.DependsOn(Pack)
		.Requires(() => NugetApiUrl)
		.Requires(() => NugetApiKey)
		.Requires(() => Configuration.Equals(Configuration.Release))
		.Executes(() =>
		{
			GlobFiles(ArtifactsDirectory, "*.nupkg")
				.NotEmpty()
				.Where(x => !x.EndsWith("symbols.nupkg"))
				.ForEach(x =>
				{
					DotNetNuGetPush(s => s
						.SetTargetPath(x)
						.SetSource(NugetApiUrl)
						.SetApiKey(NugetApiKey)
					);
				});
		});
}
