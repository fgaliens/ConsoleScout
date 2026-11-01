# Contributing to ConsoleScout

How the project is built and organised. For using it, see [README.md](README.md).

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build ConsoleScout.slnx                                      # Debug: finds the agent in its own build output
dotnet publish ConsoleScout -c Release -r osx-arm64 -o out          # what a release contains
```

A publish produces everything a release archive holds, and needs no .NET runtime to run:

- `ConsoleScout` (NativeAOT, about 12 MB) and the `csc` alias: a symlink, or `csc.cmd` on Windows; the
  name is `ShortAlias` in `ConsoleScout/ConsoleScout.csproj`;
- `appsettings.json` with the shipped defaults;
- `agent/`, the self-contained agent with Playwright's driver, published by the `PublishAgent` target.

On macOS the first start of a freshly published agent can take up to a minute while the system checks
its files; `csc -r` waits for that.

## Projects

| Project | Contents |
| --- | --- |
| `ConsoleScout` | The CLI, compiled with NativeAOT. `App/` commands and DI, `AI/` the assistant loop, prompt, model clients and page condenser, `Tools/` the tools offered to the model, `Agent/` the client for the agent, `Session/` terminal session detection, `Configuration/`, `Output/` everything shown in the console, `Donate/` the donation wallets. |
| `ConsoleScout.Agent` | The local service (ASP.NET Core, not AOT, because Playwright needs reflection): browser host, page fetching and text extraction, search engines, the question history in SQLite. |
| `ConsoleScout.Contracts` | Request and response types shared by both, with source-generated JSON. |

**Adding a tool:** implement `IAssistantTool` (name, description, JSON schema, prompt guidance, invoke)
and register it in `App/ServiceCollectionExtensions.cs`. The assistant loop and the system prompt pick it
up from the registrations.

**Adding a search engine:** implement `ISearchEngine` in the agent and register it in its `Program.cs`,
then add its name to `AgentEngines` in the CLI's `App/ServiceCollectionExtensions.cs`. An engine with its
own API can instead implement `ISearchProvider` in the CLI directly.

**Donation wallets** are constants in `ConsoleScout/Donate/DonationWallets.cs`; a wallet with an empty
address is not shown.

## Configuration layers

The CLI reads, each overriding the one before: `appsettings.json` next to the executable,
`appsettings.Debug.json` (Debug builds only, git-ignored: put real endpoints here while developing), then
`settings.json`, `preferences.json` and `secrets.json` in `~/.consolescout`, then `CONSOLESCOUT_*`
variables. Binding is strict: an unknown key is an error.

The agent reads `agent/appsettings.json`, then `~/.consolescout/agent-settings.json` (section `Agent`),
then `CONSOLESCOUT_AGENT_*` variables. `ConsoleScout.Agent --setup` opens a visible browser window on its
profile, to accept cookies or pass a check by hand; the agent must not be running.

Variables that share the `CONSOLESCOUT_` prefix without being settings: `CONSOLESCOUT_SESSION` overrides
the terminal session, `CONSOLESCOUT_RELEASE_URL` points the installers at another release server.

## How the agent is reached

The CLI starts the agent on demand as a detached process. The agent listens on `127.0.0.1` on a random
port and writes it, with a per-start token, to `~/.consolescout/agent.json` (0600). Every request needs
the token and a local `Host` header; page fetches to loopback and private addresses are refused
(`Agent:Fetch:AllowPrivateNetworks`). The agent stops after `Agent:IdleMinutes` without requests.

## Releases

`.github/workflows/build.yml` runs on every push and pull request:

1. `lint` checks `install.sh` with shellcheck. Nothing else runs when it fails.
2. `build` publishes for `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `win-x64` and `win-arm64`
   on native runners (NativeAOT compiles on the target OS). Linux builds on Ubuntu 24.04, so the binaries
   need glibc 2.39 or newer.
3. On pushes to `master`, `release` publishes `v0.1.<run number>` with the archives, `SHA256SUMS.txt`,
   `install.sh` and `install.ps1`. The installers always take the latest release.

To try the installers without GitHub, serve `<url>/latest` (the "latest release" JSON) and
`<url>/download/<tag>/<asset>` locally and set `CONSOLESCOUT_RELEASE_URL=<url>`.

## Code style

- One type per file.
- Braces around every `if`/`else`/`for`/`foreach`/`while` body, one-liners included. Enforced by
  `.editorconfig` (IDE0011) as a build warning.
- A method call is not passed as an argument; its result is assigned to a local first. Exceptions:
  string interpolation, LINQ lambdas, fluent chains, property initializers.
