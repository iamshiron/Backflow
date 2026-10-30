using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Backflow.Versioning.Tests;

/// <summary>Integration tests: real Git DAGs and GitVersion/MSBuild cannot be validated with pure unit tests.</summary>
public sealed class GitVersionTests(ITestOutputHelper output) {
    /// <summary>Checks conventional commits, merge histories, prereleases and release tags without touching real refs.</summary>
    [Fact]
    public async Task CalculateVersion_DisposableMergeHistory_ProducesExpectedVersions() {
        using var fixture = new Fixture(output);
        var repo = await fixture.CreateRepositoryAsync();
        Assert.Equal("1.2.3", await fixture.VersionAsync(repo));

        await GitAsync(repo, "switch", "-c", "dev");
        await CommitAsync(repo, "chore: Started development");
        Assert.Equal("1.2.4-dev.1", await fixture.VersionAsync(repo));

        await GitAsync(repo, "switch", "-c", "feat/fixture");
        await CommitAsync(repo, "feat: Added fixture behavior");
        Assert.Equal("1.3.0-feat-fixture.2", await fixture.VersionAsync(repo));
        await GitAsync(repo, "switch", "dev");
        await GitAsync(repo, "merge", "--no-ff", "feat/fixture", "-m", "feat: Merged fixture behavior");
        Assert.Equal("1.3.0-dev.3", await fixture.VersionAsync(repo));

        await GitAsync(repo, "switch", "-c", "fix/patch", "main");
        await CommitAsync(repo, "fix: Corrected fixture behavior");
        Assert.Equal("1.2.4-fix-patch.1", await fixture.VersionAsync(repo));

        await GitAsync(repo, "switch", "-c", "feat/breaking", "main");
        await CommitAsync(repo, "feat!: Changed fixture contract");
        Assert.Equal("2.0.0-feat-breaking.1", await fixture.VersionAsync(repo));

        await GitAsync(repo, "switch", "main");
        await GitAsync(repo, "merge", "--no-ff", "dev", "-m", "feat: Merged development");
        Assert.StartsWith("1.3.0-ci.", await fixture.VersionAsync(repo));
        await GitAsync(repo, "tag", "v1.3.0");
        Assert.Equal("1.3.0", await fixture.VersionAsync(repo));
        await GitAsync(repo, "switch", "--detach", "v1.3.0");
        Assert.Equal("1.3.0", await fixture.VersionAsync(repo));
    }

    /// <summary>Checks real builds/packages in ordinary clones, linked worktrees and submodules of linked worktrees.</summary>
    [Fact]
    public async Task BuildVersion_CheckoutTopologies_PreserveVersionAndDependencyIsolation() {
        using var fixture = new Fixture(output);
        var seed = await fixture.CreateRepositoryAsync();
        await GitAsync(seed, "switch", "-c", "dev");
        await CommitAsync(seed, "fix: Corrected fixture behavior");
        var linked = Path.Combine(fixture.Root, "linked");
        await GitAsync(seed, "worktree", "add", "-b", "build/topology", linked, "dev");
        var ordinary = Path.Combine(fixture.Root, "ordinary");
        await GitAsync(fixture.Root, "clone", "--no-local", seed, ordinary);
        await GitAsync(ordinary, "switch", "build/topology");

        var container = Path.Combine(fixture.Root, "container");
        Directory.CreateDirectory(container);
        await InitializeAsync(container);
        await CommitAsync(container, "chore: Initialized fixture container");
        var outerLinked = Path.Combine(fixture.Root, "outer-linked");
        await GitAsync(container, "worktree", "add", "-b", "fixture/container", outerLinked);
        await GitAsync(outerLinked, "-c", "protocol.file.allow=always", "submodule", "add", seed, "external/backflow");
        var submodule = Path.Combine(outerLinked, "external", "backflow");
        await GitAsync(submodule, "switch", "build/topology");

        foreach (var repo in new[] { ordinary, linked, submodule }) {
            var refs = await GitAsync(repo, "show-ref");
            Assert.Equal("1.2.4-build-topology.1", await fixture.VersionAsync(repo));
            await fixture.VerifyBuildAsync(repo);
            Assert.Equal(refs, await GitAsync(repo, "show-ref"));
        }

        var unrelated = Path.Combine(ordinary, "external", "unrelated");
        Directory.CreateDirectory(unrelated);
        var unrelatedProject = Path.Combine(unrelated, "Unrelated.csproj");
        await File.WriteAllTextAsync(unrelatedProject, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await RunAsync(unrelated, "dotnet", "restore", unrelatedProject);
        using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(unrelated, "obj", "project.assets.json")));
        Assert.DoesNotContain(assets.RootElement.GetProperty("libraries").EnumerateObject(),
            item => item.Name.StartsWith("GitVersion.", StringComparison.Ordinal));
        Assert.Equal("", (await RunAsync(unrelated, "dotnet", "msbuild", unrelatedProject,
            "-nologo", "-getProperty:GitVersion_CommandLineArguments")).Trim());
    }

    private static Task<string> GitAsync(string directory, params string[] arguments) => RunAsync(directory, "git", arguments);

    private static async Task InitializeAsync(string directory) {
        await GitAsync(directory, "init", "--initial-branch=main");
        await GitAsync(directory, "config", "user.email", "fixture@example.invalid");
        await GitAsync(directory, "config", "user.name", "Version fixture");
        await GitAsync(directory, "config", "commit.gpgsign", "false");
        await GitAsync(directory, "config", "tag.gpgsign", "false");
    }

    private static Task<string> CommitAsync(string directory, string message) =>
        GitAsync(directory, "commit", "--allow-empty", "-m", message);

    private static async Task<string> RunAsync(string directory, string executable, params string[] arguments) {
        var start = new ProcessStartInfo(executable) {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var variable in new[] { "GITHUB_ACTIONS", "GITHUB_REF", "GITHUB_HEAD_REF", "GITHUB_BASE_REF", "TF_BUILD", "APPVEYOR", "GITLAB_CI", "TEAMCITY_VERSION", "JENKINS_URL" })
            start.Environment.Remove(variable);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start fixture process.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try {
            await process.WaitForExitAsync(timeout.Token);
        } catch (OperationCanceledException) {
            process.Kill(entireProcessTree: true);
            throw;
        }
        var result = await stdout;
        Assert.True(process.ExitCode == 0, $"{executable} {string.Join(' ', arguments)} in {directory}\n{result}\n{await stderr}");
        return result;
    }

    private sealed class Fixture : IDisposable {
        private readonly string _sourceRoot;

        internal Fixture(ITestOutputHelper output) {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GitVersion.yml")))
                directory = directory.Parent;
            _sourceRoot = directory?.FullName ?? throw new InvalidOperationException("Cannot locate version policy.");
            var temporaryRoot = Directory.Exists("/tmp/opencode") ? "/tmp/opencode" : Path.GetTempPath();
            Root = Path.Combine(temporaryRoot, $"backflow-version-fixture-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            output.WriteLine($"Disposable fixture root: {Root}");
        }

        internal string Root { get; }

        internal async Task<string> CreateRepositoryAsync() {
            var repo = Path.Combine(Root, "seed");
            var project = Path.Combine(repo, "src", "Fixture");
            Directory.CreateDirectory(project);
            foreach (var file in new[] { "GitVersion.yml", "Directory.Build.props", "Directory.Packages.props", "LICENSE" })
                File.Copy(Path.Combine(_sourceRoot, file), Path.Combine(repo, file));
            await File.WriteAllTextAsync(Path.Combine(repo, ".gitignore"), "**/bin/\n**/obj/\n");
            await File.WriteAllTextAsync(Path.Combine(project, "Fixture.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(project, "Marker.cs"),
                "namespace Backflow.Versioning.Fixture; /// <summary>Versioning fixture marker.</summary>\npublic sealed class Marker;");
            await InitializeAsync(repo);
            await GitAsync(repo, "add", ".");
            await CommitAsync(repo, "chore: Initialized version fixture");
            await GitAsync(repo, "tag", "v1.2.3");
            return repo;
        }

        internal async Task<string> VersionAsync(string repo) {
            var json = await RunAsync(_sourceRoot, "dotnet", "tool", "run", "dotnet-gitversion", "--", repo,
                "/config", Path.Combine(repo, "GitVersion.yml"), "/nocache", "/nonormalize", "/nofetch", "/verbosity", "Quiet", "/output", "json");
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("SemVer").GetString() ?? throw new InvalidOperationException("Missing semantic version.");
        }

        internal async Task VerifyBuildAsync(string repo) {
            var project = Path.Combine(repo, "src", "Fixture", "Fixture.csproj");
            await RunAsync(repo, "dotnet", "build", project, "--configuration", "Release", "--verbosity", "quiet");
            using var generated = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, "src", "Fixture", "obj", "gitversion.json")));
            var version = await VersionAsync(repo);
            Assert.Equal(version, generated.RootElement.GetProperty("SemVer").GetString());
            Assert.Equal((await GitAsync(repo, "rev-parse", "HEAD")).Trim(), generated.RootElement.GetProperty("Sha").GetString());
            var assemblyPath = Path.Combine(repo, "src", "Fixture", "bin", "Release", "net10.0", "Fixture.dll");
            var context = new AssemblyLoadContext($"version-fixture-{Guid.NewGuid():N}", isCollectible: true);
            try {
                using var assemblyStream = File.OpenRead(assemblyPath);
                var assembly = context.LoadFromStream(assemblyStream);
                Assert.Equal(generated.RootElement.GetProperty("AssemblySemVer").GetString(), assembly.GetName().Version?.ToString());
                Assert.Contains(version, assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
            } finally {
                context.Unload();
            }
            var packages = Path.Combine(repo, "packages");
            await RunAsync(repo, "dotnet", "pack", project, "--configuration", "Release", "--no-build", "--output", packages, "--verbosity", "quiet");
            using var archive = ZipFile.OpenRead(Path.Combine(packages, $"Fixture.{version}.nupkg"));
            using var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
            var specification = XDocument.Load(nuspec);
            Assert.Equal(version, specification.Descendants().Single(element => element.Name.LocalName == "version").Value);
            Assert.DoesNotContain(specification.Descendants(), element => element.Name.LocalName == "dependency" &&
                element.Attribute("id")?.Value == "GitVersion.MsBuild");
        }

        /// <inheritdoc/>
        public void Dispose() {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, recursive: true);
        }
    }
}
