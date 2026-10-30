# Build and package versions

Backflow derives versions from its own Git history with GitVersion. There is no hardcoded release version, `next-version`, disabled version task, or fallback that converts a failed calculation into a release. The local CLI and private MSBuild package were resolved through `dotnet tool install GitVersion.Tool` and `dotnet add package GitVersion.MsBuild`; both are pinned to 6.8.2.

## Policy

`GitVersion.yml` extends the supported `GitHubFlow/v1` workflow:

- `dev` produces `-dev.N` prereleases.
- `feat/*`, `fix/*`, `build/*`, and other configured work branches produce branch-qualified prereleases such as `-feat-example.N` and `-fix-example.N`. Other branches also retain a prerelease label.
- Conventional `feat:` commits select a minor increment, `fix:` selects a patch, and `type!:` or a `BREAKING CHANGE:` footer selects a major increment. Existing `+semver:` directives remain supported. Commits are evaluated across merge histories; integrate branches with merge commits.
- Untagged `main` builds have a `-ci.N` label. A reviewed SemVer tag such as `v1.3.0` produces `1.3.0`, including detached checkouts of that tag. Merely mentioning `main` in this policy does not authorize changing it or creating release tags.
- Prerelease counters derive from commits since the version source, not timestamps or a CI run number. The repository currently has no release tags; GitVersion's initial prerelease base is not evidence that a release has been published.

Assembly versions and file versions come from GitVersion's numeric version fields. Informational versions retain branch and commit provenance. Package versions use GitVersion's `FullSemVer`; the configured continuous-delivery development/feature branches produce monotonically advancing prerelease identifiers. Neither assembly names nor namespaces are overridden: the existing `Shiron.Backflow` and `Shiron.Backflow.Ext.DI` identities remain intact.

## Build integration and repository boundaries

`Directory.Build.props` adds a private `GitVersion.MsBuild` reference only to this repository's `src/` projects. Tests, samples, benchmarks, and `external/` projects do not acquire that reference or its CLI options. The path check is relative to this file, not the consuming solution, so building Backflow from another solution still uses Backflow's policy and history. `GitVersionPath` and the configuration file are rooted here, not in the caller's `SolutionDir`.

The supported MSBuild options are deliberately enabled:

- `GitVersion_NoCacheEnabled`: bypass GitVersion's cache-key path handling, which misidentifies submodules under a linked worktree's `modules` directory in this release. This does **not** disable version calculation.
- `GitVersion_NoNormalizeEnabled`: do not rewrite repository refs for a CI-specific checkout layout.
- `GitVersion_NoFetchEnabled`: do not fetch or mutate remote state during a build.

Full Git history and tags must already be present. CI checkouts use `fetch-depth: 0`. Restore initializes the private MSBuild package; ordinary builds do not require the CLI tool to be restored separately. The CLI is for inspection and policy tests. A detached non-release submodule checkout may have a detached/unknown prerelease label; the commit and version source remain from Backflow, not its consumer.

The pinned nested `external/lib` source and gitlink are unchanged. Its older Collections project naming and any duplicate-assembly integration seam with a consuming application's different shared-library revision are separate concerns, not solved by versioning.

## Reproduce locally

```bash
git submodule update --init --recursive
dotnet tool restore
dotnet gitversion /nocache /nonormalize /nofetch /output json
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --verbosity minimal
dotnet test tests/Versioning/Backflow.Versioning.Tests.csproj --configuration Release --verbosity minimal
dotnet build samples/Backflow/Shiron.Backflow.Samples.csproj --configuration Release
dotnet list samples/Backflow/Shiron.Backflow.Samples.csproj package --vulnerable --include-transitive
```

The separate version-policy project is intentionally invoked explicitly; it is not added to or removed from the application solution. These are integration tests because Git DAG traversal, GitVersion, generated assembly metadata, and NuGet packing require actual repository/tool boundaries. They create disposable repositories beneath `/tmp/opencode` when available, otherwise the OS temporary directory. Only fixture repositories receive commits, branches, tags, or local-file submodule configuration. Fixtures are deleted after each test, and the real repository's refs are never changed by tests.

The tests establish exact versions from fixture tag `v1.2.3`: `1.2.4-dev.1`, `1.3.0-feat-fixture.2`, `1.3.0-dev.3` after a merge commit, `1.2.4-fix-patch.1`, and `2.0.0-feat-breaking.1`. A fixture-only release merge/tag produces `1.3.0`. A second history produces the same `1.2.4-build-topology.1` in an ordinary clone, linked worktree, and submodule inside a linked worktree. Each topology is actually built and packed; generated JSON, assembly version/provenance, package version, private dependency exclusion, and unchanged refs are checked. An unrelated nested project's restore confirms no GitVersion dependency/options leak into `external/`.

Local Linux validation passed the full Release solution build, all 509 existing library/DI tests, and both separate versioning integration tests. The sample builds with its original dependency but still emits the six unsuppressed advisory findings below. Local packages for both existing source libraries were inspected: their versions match GitVersion, Ext.DI depends on that same Backflow package version, and neither exposes GitVersion.MsBuild as a consumer dependency. Windows/macOS fixture runs and remote CI results are not claimed by this local validation.

Formatting commands for this change:

```bash
dotnet format Shiron.Backflow.slnx --include tests/Versioning --exclude external/lib
dotnet format tests/Versioning/Backflow.Versioning.Tests.csproj
```

## Package preparation, not publication

Build and test first, then prepare packages locally:

```bash
dotnet pack src/Backflow/Shiron.Backflow.csproj --configuration Release --output artifacts/packages
dotnet pack src/Backflow.Ext.DI/Shiron.Backflow.Ext.DI.csproj --configuration Release --output artifacts/packages
```

Inspect the `.nuspec`, assembly metadata, version, and commit provenance before any separately authorized publication. Keep CLI and MSBuild versions aligned on future package-manager updates. A release requires an approved tag/history and clean full checkout; do not override `Version`/`PackageVersion` or disable GitVersion to manufacture a release. No package publication or release tag creation is part of this change.

## Outstanding sample dependency vulnerability

The existing sample dependency `SixLabors.ImageSharp` 3.1.12 has six reported high/moderate advisories, including GHSA-j3p4-wp97-rph4. This is **not fixed** or suppressed here. NuGet resolved the latest stable 4.1.3 and the latest 3.x 3.2.0 during upgrade validation; both fail Release compilation without a Six Labors license. The latest 3.1.x still resolves to vulnerable 3.1.12. No license was available in this environment, so the original dependency is retained rather than bypassing enforcement or committing an unbuildable upgrade. A supplied valid license or an explicitly approved sample-library replacement is required before this security follow-up can be completed. The existing warnings-as-errors quality gate continues to surface these advisories.
