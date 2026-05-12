# ComputerUseWorkbenchRuntime.cs Change History

## Use module-owned run-file route (2026-04-26)
- Updated run-file URLs to use `/computer-use/runfiles/...` so installed module routes do not collide with host root routes.

## Harden installed runner launch environment (2026-04-26)
- Added `ComputerUseHarness:RunnerProjectPath` support so deployed installations can point the web harness at an installed runner project path instead of relying only on source-tree ancestor discovery.
- Changed runner launch to use `ProcessStartInfo.ArgumentList` so paths and quoted values are passed as exact process arguments instead of shell-style concatenated text.
- Added per-run .NET CLI, user-profile, NuGet cache, and app-data environment variables so IIS-hosted runs do not depend on inaccessible `systemprofile` paths.
