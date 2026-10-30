# Licensing audit and third-party boundaries

Audit date: 2026-10-30. This is a repository-boundary audit, not an exhaustive
transitive dependency notice bundle or legal compatibility certification.

## Project-owned code

Backflow already contained the full GNU Affero General Public License version 3
text. The copyright holder authorized explicit `AGPL-3.0-only` licensing for
project-owned code. The runtime, DI, ASP.NET Core and S3 extensions, tests, samples,
build tooling, documentation and project-owned React/client ecosystem are covered.
They are a workflow ecosystem, not tiny MIT utility libraries. No later-version
grant is made by this project; the example application notice in the unmodified
canonical license text is not the project's version-choice notice.

NuGet metadata is inherited from the root `Directory.Build.props`: the SPDX
`PackageLicenseExpression` is `AGPL-3.0-only`, and source packages under `src` also
include `LICENSE`. There is no conflicting `PackageLicenseFile` declaration.
React/package metadata is maintained with the React/client implementation.

The inspected history identifies Shiron under two email addresses. Authorship
records do not establish assignment or ownership of imported code; no specific
additional rights holder was identified in this audit. Preserve any existing
third-party notices. Existing recipients' rights are not revoked.

## Shared library submodule

`external/lib` is the separate https://github.com/iamshiron/lib repository, pinned
at `be9c835305de5d264e12b118f6fdf0be8aa450c3` at audit time. Its upstream `LICENSE`
contains the full GNU AGPL version 3 text. This audit does not infer a later-version
grant, change its license, or claim its packages are owned by Backflow.
The runtime references its Collections project. Preserve upstream notices and
check the resolved submodule's own packaging when distributing it.

## Other dependencies and remaining release checks

Direct .NET package versions are recorded in `Directory.Packages.props`; package
references identify their consumers. Resolved dependencies retain their licenses.
Examples include AWS SDK S3, Microsoft extension/framework packages, and test/build
tools. This audit does not replace their license texts or assert a single license
for every dependency.

The sample's `SixLabors.ImageSharp` 3.1.12 uses Six Labors' split-license terms;
do not assume it is MIT or covered by Backflow's AGPL grant. Review the exact
package's license against the user's circumstances before use/distribution.
See `docs/versioning.md` for the separately recorded security/license-enforcement
upgrade blocker. No dependency upgrade or license-enforcement bypass was made.

Before releasing a binary, container or browser bundle, collect the actual resolved
direct/transitive license texts and required notices (including native assets).
Publish corresponding source for the exact revision and modifications where
required, with relevant build instructions, and provide the required remote source
offer for modified network versions. This audit does not certify compliance of
every downstream combination or deployment.
