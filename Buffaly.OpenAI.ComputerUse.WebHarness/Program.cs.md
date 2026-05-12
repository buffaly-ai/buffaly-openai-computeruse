# Program.cs Change History

## Use ComputerUse web module adapter (2026-04-26)
- Updated the standalone harness host to instantiate `ComputerUseWorkbenchModule` through the same module adapter used by installed Buffaly web hosts.
- Design Decision: keep standalone development hosting and installable module startup on the same route-registration path.