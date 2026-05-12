# Sanitized Export Report

Source: C:\dev\buffaly.tools\buffaly.openai.computeruse
Destination: C:\dev\buffaly-ai\buffaly-openai-computeruse
Solution: 
Included files: 42
Excluded files: 877
Included allowed binaries: 0
Manual review candidates: 4
Secret pattern hits: 7

## Included Allowed Binaries
None.

## Manual Review Candidates
- Buffaly.OpenAI.ComputerUse\ComputerRequestBuilder.cs
- Buffaly.OpenAI.ComputerUse\ComputerResponseParser.cs
- Buffaly.OpenAI.ComputerUse.WebHarness\appsettings.json
- Buffaly.OpenAI.ComputerUse.WebHarness\Properties\launchSettings.json

Reviewed: request/response parser/builders contain protocol DTO logic only. Web harness appsettings was sanitized to avoid local machine paths; launchSettings contains localhost-only development profile.

## Secret Pattern Hits
- Buffaly.OpenAI.ComputerUse\OpenAIComputerUseFacade.cs
- Buffaly.OpenAI.ComputerUse.Runner\Program.cs
- Buffaly.OpenAI.ComputerUse.Smoke\Program.cs
- Buffaly.OpenAI.ComputerUse.Tests\OpenAIComputerUseFacadeTests.cs
- Buffaly.OpenAI.ComputerUse.WebHarness\ComputerUseWorkbenchJsonWsService.cs
- Buffaly.OpenAI.ComputerUse.WebHarness\ComputerUseWorkbenchRuntime.cs
- Buffaly.OpenAI.ComputerUse.WebHarness\wwwroot\js\computer-use-workbench.js

Reviewed: hits are API-key/environment-variable/configuration handling references and UI status labels, not embedded secret values.

## AttributionCheck
Before commit/push, run: powershell -NoProfile -ExecutionPolicy Bypass -File C:\\dev\\buffaly-ai\\scripts\\Test-PrePushAttribution.ps1 -RepoRoot <repo-root>
