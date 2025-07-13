using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Serilog;
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
                .SetVersion(GitVersion.MajorMinorPatch)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetProperty("PackageReleaseNotes", $"Built from commit {GitVersion.Sha[..7]}"));
        });

    Target PublishArtifacts => d => d
        .DependsOn(Pack)
        .OnlyWhenStatic(() => IsServerBuild)
        .Executes(() =>
        {
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");
            Log.Information("Publishing {Count} artifacts to Azure Pipelines: {Packages}", packages.Count, packages);
            
            if (packages.Count == 0)
            {
                Log.Warning("No .nupkg files found in artifacts directory");
                return;
            }

            // Upload artifacts for Azure Pipelines with proper parameters
            AzurePipelines.Instance?.UploadArtifacts("packages", "packages", ArtifactsDirectory);
        });
    
    Target Publish => d => d
        .DependsOn(PublishArtifacts)
        .Requires(() => NuGetApiKey)
        .OnlyWhenStatic(() => Repository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");
            Log.Debug("Found {Count} packages to publish: {Packages}", packages.Count, packages);
            
            if (packages.Count == 0)
            {
                Log.Error("No .nupkg files found for publishing. Make sure Pack target was executed.");
                return;
            }
            
            packages.ForEach(package =>
            {
                Log.Information("Publishing package: {Package}", package);
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(NuGetApiKey)
                    .SetSkipDuplicate(true));
            });
        });

    Target PublishOnly => d => d
        .Requires(() => NuGetApiKey)
        .OnlyWhenStatic(() => Repository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            // This target assumes packages are already in artifacts directory
            // Use this when you want to publish without rebuilding
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");
            
            if (packages.Count == 0)
            {
                // Try to find packages in source directories as fallback
                packages = SourceDirectory.GlobFiles("**/bin/**/*.nupkg");
                Log.Information("No packages in artifacts, found {Count} in source directories", packages.Count);
            }
            
            Log.Information("Found {Count} packages to publish: {Packages}", packages.Count, packages);
            
            if (packages.Count == 0)
            {
                Log.Error("No .nupkg files found. Run Pack target first or ensure packages exist.");
                return;
            }
            
            packages.ForEach(package =>
            {
                Log.Information("Publishing package: {Package}", package);
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(NuGetApiKey)
                    .SetSkipDuplicate(true));
            });
        });
}
