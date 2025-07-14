using CSharpFunctionalExtensions;
using DotnetPackaging.Deployment;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
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
    public static int Main() => Execute<Build>(x => x.Publish);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter] [Secret] readonly string NuGetApiKey;
    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository Repository;
    [Solution] readonly Solution Solution;
    [Parameter("Force publish even if not a server build")] readonly bool Force = false;
    
    Target Publish => d => d
        .Requires(() => NuGetApiKey)
        .OnlyWhenStatic(() => Repository.IsOnMainOrMasterBranch() || Force)
        .Executes(async () =>
        {
            var project = Solution.GetProject("DeflateBlockCompressor").AsMaybe().ToResult("Project 'DeflateBlockCompressor' not found in the solution.");
            await project.Map(p => Deployer.Instance.PublishNugetPackages([p.Path.ToString()], GitVersion.MajorMinorPatch, NuGetApiKey))
                .Tap(() => Log.Information("NuGet packages published successfully."))
                .TapError(err => Assert.Fail(err));
        });
}