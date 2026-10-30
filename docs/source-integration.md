# Shared-library source integration

Backflow builds independently with its pinned `external/lib` submodule. Initialize
it using `git submodule update --init --recursive` before a standalone build.

Applications that already compile Shiron.Lib.Collections from source can select
that same project with the MSBuild property `BackflowCollectionsProject`. Pass an
absolute project path as a global build property, or as `AdditionalProperties` on
the application's Backflow project reference. This prevents loading two independently
compiled copies of the same shared assembly into one application graph. The property
changes source linkage only; it does not alter nodes, execution, caching or public
contracts. The caller must validate compatibility against its selected library.

Without this explicit property, standalone builds always use the pinned dependency.
No application directory, domain, framework, or repository name is assumed.
