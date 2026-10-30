# Shared-library source integration

Backflow builds independently with its pinned `external/lib` submodule. Initialize
it using `git submodule update --init --recursive` before a standalone build.

Applications that already compile Shiron.Lib.Collections from source can select
that same project with the MSBuild property `BackflowCollectionsProject`. For
zero-configuration builds, put `Backflow.Collections.props` in an ancestor directory
of the Backflow checkout:

```xml
<Project>
  <PropertyGroup>
    <BackflowCollectionsProject Condition="'$(BackflowCollectionsProject)' == ''">$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)external/lib/src/Collections/Lib.Collections.csproj'))</BackflowCollectionsProject>
  </PropertyGroup>
</Project>
```

Backflow imports the nearest ancestor's file during project evaluation, before
its source references are declared. This is deliberately a narrow, opt-in consumer
configuration file, not an import of a consumer's general Directory.Build.props.
Adapt the path to the consumer repository; Backflow assumes no application name or
directory layout. The conditional default preserves an explicitly supplied environment
variable or global `-p:BackflowCollectionsProject=/absolute/project.csproj` override.

The property changes source linkage only; it does not alter nodes, execution,
caching or public contracts. The caller must validate library compatibility.
Without a consumer file or override, standalone builds use the pinned dependency.

Do not inject this default with ProjectReference `AdditionalProperties`: that
creates different MSBuild project identities for the same physical Backflow and
Collections outputs, and restore may walk a different graph from build. A local
evaluation-time default introduces no extra global property. A command-line override
must be supplied consistently to the whole consumer graph, including any direct
references to the chosen Collections project; it should not be removed on just one
dependency edge, which would itself split the shared project's identity.
