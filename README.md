# Buffaly OpenAI Computer Use

Buffaly OpenAI Computer Use contains the integration layer, runner, smoke tests, tests, and web harness for experimenting with OpenAI computer-use workflows in Buffaly.

Buffaly is a field-tested runtime for high-trust agents, developed by Matt Furnari. This repository is part of the public `buffaly-ai` source release and is intended for inspection, debugging, plugin/tool development, partner integration, and LLM-assisted understanding.

## How this fits into Buffaly

It shows how a model/tool capability can be packaged as a Buffaly module with inspectable runtime state and separate harnesses.

## What is in this repository

- Computer-use request/response contracts
- Runner executable
- Smoke and unit tests
- Web harness
- Module package prompts

## Repository map

- `Buffaly.OpenAI.ComputerUse.Runner/Buffaly.OpenAI.ComputerUse.Runner.csproj`
- `Buffaly.OpenAI.ComputerUse.Smoke/Buffaly.OpenAI.ComputerUse.Smoke.csproj`
- `Buffaly.OpenAI.ComputerUse.Tests/Buffaly.OpenAI.ComputerUse.Tests.csproj`
- `Buffaly.OpenAI.ComputerUse.WebHarness/Buffaly.OpenAI.ComputerUse.WebHarness.csproj`
- `Buffaly.OpenAI.ComputerUse/Buffaly.OpenAI.ComputerUse.csproj`

## Build

This repository is source-visible first. The installer is still the recommended path for normal use, but the source is here so developers and partners can inspect behavior, debug integrations, and build plugins/tools.

```powershell
# From this repository root
dotnet restore buffaly.openai.computeruse.sln
dotnet build buffaly.openai.computeruse.sln --configuration Release
```

Some repositories include partner/closed support binaries under `lib/` so the public source can compile without immediately open-sourcing every historical dependency. More dependencies may be opened over time as time allows.

## Configuration and secrets

OpenAI credentials must be supplied by environment or local secrets. Do not commit API keys, run states, screenshots, or captured desktop/customer data.

If you add examples, keep them as placeholders. Never commit PHI, customer data, credentials, OAuth tokens, API keys, bearer tokens, connection strings with passwords, private browser state, or live run/session artifacts.

## What is intentionally not included

Private OpenAI keys, run logs, screenshots, and customer-specific automation playbooks are not included.

Some domain packs, healthcare workflows, customer-specific connectors, deployment assets, implementation playbooks, sensitive demos/data, and private operational configuration remain separate from the public core.

## Using this source

The source is provided to make Buffaly inspectable and useful for builders who want to understand the runtime, debug integrations, or create plugins and tools. For most users, the installer/runtime package is the fastest path. If you are building proprietary products, redistributing Buffaly, or need supported deployment terms, use the commercial licensing route below.

## Licensing

Buffaly core is GPLv3 by default. If your organization needs different terms for proprietary use, redistribution, or supported deployment, contact us for commercial licensing.

Buffaly is developed by Matt Furnari.

See [LICENSING.md](LICENSING.md) and [CONTRIBUTING.md](CONTRIBUTING.md).

## Commercial licensing

Commercial licensing is available for organizations that need different terms for proprietary use, redistribution, private embedding, hosted product use, or supported deployment. Open a GitHub issue in this repository with the label `commercial-licensing` to start that discussion.

## Contributions

Major external code contributions are expected to require a Contributor License Agreement (CLA). Small documentation fixes, typo fixes, and issue reports may be handled without a CLA at the maintainer's discretion.
