using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[AzurePipelines(
    AzurePipelinesImage.UbuntuLatest, 
    InvokedTargets = [nameof(Publish)], AutoGenerate = true, 
    ImportSecrets = ["NuGetApiKey"],
    FetchDepth = 0, ImportVariableGroups = ["api-keys"])
]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Publish);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [Parameter] [Secret] readonly string NuGetApiKey;
    [Parameter("NuGet Source URL for publishing packages")]
    readonly string NuGetSource = "https://api.nuget.org/v3/index.json";
    
    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository Repository;
    
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => d => d
        .Before(Pack)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(d => d.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Pack => d => d
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(SourceDirectory / "DeflateBlockCompressor" / "DeflateBlockCompressor.csproj")
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVersion(GitVersion.MajorMinorPatch)
                .SetProperty("PackageReleaseNotes", $"Built from commit {GitVersion.Sha[..7]}"));
        });
    
    Target Publish => d => d
        .DependsOn(Pack)
        .Requires(() => NuGetApiKey)
        .OnlyWhenStatic(() => Repository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");
            
            packages.ForEach(package =>
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(NuGetApiKey)
                    .SetSkipDuplicate(true));
            });
        });
}
