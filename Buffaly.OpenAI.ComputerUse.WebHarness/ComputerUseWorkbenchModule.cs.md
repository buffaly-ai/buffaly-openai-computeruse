# ComputerUseWorkbenchModule.cs Change History

## Convert to installable web module (2026-04-26)
- Converted the workbench module into an `IBuffalyWebModule` implementation named `ComputerUse`.
- Design Decision: runtime startup registers JsonWs and module-owned run-file routes; install-time copying is handled by the published web-module convention instead of `InstallArtifacts`.