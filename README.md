# ConsoleScout

A question-answering assistant for the terminal. Ask a question in plain words; the model answers in the
console, searching the web and reading pages when the answer depends on something current. Answers
carry no citation markers or source lists; a link appears in the text only when it is useful in itself.

```
$ csc when was .NET 10 released
```

The model knows the current date and time and your operating system, so answers about "today" and
commands for your shell come out right.

## Quick start

```
# macOS, Linux
wget -qO- https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh

# Windows (PowerShell)
irm https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.ps1 | iex

csc --set-key main          # asks for the API key, hidden
csc what is the latest version of Node.js
```

`curl -fsSL <url> | sh` works as well as `wget`. The installer picks the build for your system and
processor, asks where to install (Enter keeps the default) and puts `csc` on your `PATH`. Run the same
command again to update: it asks nothing then, and keeps your settings. Google Chrome is used for web
access when installed; otherwise Chromium is downloaded on first use. Any OpenAI-compatible chat API
works; models are set in `~/.consolescout/settings.json` (see [Configuration](#configuration)).

## Usage

`ConsoleScout` and `csc` are the same program.

| Command | What it does |
| --- | --- |
| `csc` | Asks for a question. While a model key is missing, shows this guide and the setup status instead. |
| `csc --status` | The setup state: configuration, model keys, agent. Exit code 1 while a question cannot be answered. |
| `csc --readme` | This documentation in full. |
| `csc <question>` | Answers the question. |
| `csc -l <question>`, `--long` | A detailed answer. |
| `csc -s <question>`, `--short` | The shortest answer. Default length: `preferences.answerLength`. |
| `csc -c <question>`, `--with-context` | Follows up on the last answer in this terminal. `-c n2 …` on the one two before it, `-c x42 …` on answer 42 from any terminal. See [Follow-up questions](#follow-up-questions). |
| `csc -k main`, `--set-key main` | Saves a model's API key to `~/.consolescout/secrets.json`; asks for it without echoing. `-k main=<key>` takes it from the argument. Models: `main`, `summarizer`. Repeatable. |
| `csc -r`, `--run-agent` | Starts the agent and waits until its browser is ready. Also the setup step on a new machine. |
| `csc --donate` | Lists the networks you can donate on. |
| `csc --donate eth` | Shows that network's address and QR code. Networks by ticker: `btc`, `eth`, `usdt`, `ton`, `sol`. |
| `csc --no-donate` | Stops showing the donation hint while the model thinks. |

A question, `--set-key`, `--run-agent`, `--donate`, `--no-donate`, `--status` and `--readme` cannot be combined.

> **zsh and `?`:** zsh treats `?` and `*` in arguments as file patterns and stops with
> `zsh: no matches found` before the program even starts. Add `alias csc='noglob csc'` to `~/.zshrc`,
> or quote the question: `csc "what is new in .NET 10?"`. Running `csc` alone and typing the question
> at the prompt also avoids it. bash, PowerShell and cmd pass such arguments through unchanged.

> **About the name `csc`:** it is also the name of the C# compiler (installed by Mono, and present in
> Visual Studio's Developer Command Prompt). If one is on your `PATH`, whichever comes first wins. To pick
> another alias, change `ShortAlias` in `ConsoleScout/ConsoleScout.csproj`.

## Models

| Model | Role |
| --- | --- |
| `main` | Answers the question and decides what to search and which pages to read. |
| `summarizer` | Optional, cheaper. Condenses every fetched page to what bears on the question, so `main` reads a short digest instead of thousands of characters of page text. |

Without a `models.summarizer` section, pages reach the main model as raw text. If the summarizer fails
on a page, that page falls back to raw text too. Turn condensing off with `tools.webFetch.condense: false`.

## Tools

The model is offered these tools; each can be turned off in `tools`, and a disabled tool is not mentioned
in the prompt at all.

- **`web_search`**: titles, links and snippets. Engines are tried in the order of
  `tools.webSearch.providers` (`google`, `duckduckgo`); one that answers with a CAPTCHA is skipped for
  the rest of the run.
- **`web_fetch`**: reads up to `maxBatchSize` pages in one call, in parallel, and returns them as one
  result, each under its URL. A page that cannot be read is reported in its place without failing
  the others. Pages are rendered in a browser when plain HTTP is not enough.

## Follow-up questions

Every answer is saved with an id, shown under it as `x42`. `-c` hands the model the conversation so far,
so a question can build on earlier answers:

```
csc how do I list open ports on macOS
csc -c and only the ones listening on IPv6      # follows up on the last answer in this terminal
csc -c n1 and on Linux                          # on the one before it
csc -c x42 what about Windows                   # on answer 42, from any terminal
```

- **Terminal session.** `-c` and `-c n<N>` look only at questions asked in the same terminal tab (or tmux
  pane): its Unix session, which subshells and pipelines share. On Windows, the shell that started `csc`.
  Set `CONSOLESCOUT_SESSION` to share or separate sessions on purpose.
- **Depth.** The model gets up to `history.maxContextTurns` (5) earlier questions and answers of the thread.
- **Parsing.** Only `x<digits>` and `n<digits>` right after `-c` are a reference; anything else, a bare
  number included, belongs to the question. `csc -c 3 apples` asks about "3 apples".
- **Storage.** The agent keeps the history in `~/.consolescout/history.db` (SQLite, owner-only) and
  removes entries after `Agent:History:KeepDays` (30). `history.enabled: false` turns it off.

## Configuration

Settings are read through `IConfiguration` from these sources; each overrides the ones before it:

1. `appsettings.json` next to the executable: the shipped defaults, replaced by every update.
2. `appsettings.Debug.json`: Debug builds only, not tracked in git. For your real endpoints and model names.
3. `~/.consolescout/settings.json`: **your settings**, such as models and search providers. Updates leave
   it alone; an update that finds an edited `appsettings.json` moves the edits here.
4. `~/.consolescout/preferences.json`: choices made from the command line, such as `--no-donate`.
5. `~/.consolescout/secrets.json`: API keys, written by `--set-key`. Owner-only (0600). A key in
   `settings.json` works too, but this file is meant to stay private.
6. Environment variables prefixed `CONSOLESCOUT_`, with `__` between sections:
   `CONSOLESCOUT_MODELS__MAIN__APIKEY`, `CONSOLESCOUT_TOOLS__WEBSEARCH__PROVIDERS__0=duckduckgo`.

All files share the shape of `appsettings.json`. Unknown or misspelled settings are an error, not
silently ignored.

```json
{
  "models": {
    "main":       { "endpoint": "https://api.example.ai/v1", "name": "smart-model", "maxToolRounds": 6 },
    "summarizer": { "endpoint": "https://api.example.ai/v1", "name": "cheap-model" }
  },
  "tools": {
    "webSearch": { "enabled": true, "providers": [ "google", "duckduckgo" ], "maxResults": 5 },
    "webFetch":  { "enabled": true, "maxChars": 8000, "maxBatchSize": 5, "maxBatchChars": 24000,
                   "condense": true, "condensedMaxWords": 250 }
  },
  "agent": { "autoStart": true, "startupTimeoutSeconds": 30, "requestTimeoutSeconds": 60 },
  "output": { "format": "markdown" },
  "preferences": { "answerLength": "short", "showDonateHint": true },
  "history": { "enabled": true, "maxContextTurns": 5 }
}
```

`output.format` is `markdown` (rendered in the console) or `plain`. `flattenContentArrays: true` on a
model fixes providers that return `message.content` as an array of chunks.

## Agent

Web access and the question history go through **ConsoleScout.Agent**, a separate local service that
drives a real browser with Playwright and keeps the history in SQLite. It is a separate process because
Playwright cannot be compiled with NativeAOT, which the CLI uses to start instantly; it also keeps the
browser warm between questions. The CLI starts it in the background as a question begins, so it is
usually up by the time it is needed.

- **Starts on demand.** The CLI starts it when a question needs the web and it is not running, and it
  stops itself after `Agent:IdleMinutes` (60 minutes in the shipped settings) without requests. `csc -r` starts it explicitly.
- **Browser.** Installed Google Chrome by default (`Agent:Browser:Channel`). If Chrome is missing,
  Chromium is downloaded on first use; `csc -r` is a good moment for that. Playwright 1.63 no longer
  ships Chromium for macOS 13, so there Chrome is required.
- **Google** answers headless browsers with a CAPTCHA more often than not; the search then falls through
  to DuckDuckGo. Running `ConsoleScout.Agent --setup` opens a visible window on the agent's browser
  profile to accept cookies or pass a check by hand; it needs the agent to be stopped first. CAPTCHAs
  are never worked around automatically.
- **Security.** The service listens on `127.0.0.1` only, on a random port announced in
  `~/.consolescout/agent.json` (0600) together with a per-start token that every request must carry. It
  also checks the `Host` header against DNS rebinding, and refuses to fetch loopback and private-network
  addresses (`Agent:Fetch:AllowPrivateNetworks`).
- **Its own settings** are in `agent/appsettings.json`; yours go in `~/.consolescout/agent-settings.json`
  (section `Agent`), which updates leave alone. Variables prefixed `CONSOLESCOUT_AGENT_` override both
  (e.g. `CONSOLESCOUT_AGENT_Agent__Browser__Headless=false`). Its log is `~/.consolescout/agent.log`.

## Installation details

- **macOS and Linux** (`install.sh`): installs to `~/.local/share/consolescout` and puts small `csc` and
  `ConsoleScout` launchers in `~/.local/bin`. If that is not on your `PATH`, it offers to add it to your
  shell's rc file, and in zsh to add `alias csc='noglob csc'`, both in one marked `ConsoleScout` block.
  Options: `--yes` (no questions), `--force` (reinstall the same version), `--dir`, `--bin-dir`; pass them
  with `… | sh -s -- --yes`. Linux builds need glibc 2.35 or newer (Ubuntu 22.04, Debian 12 and up).
- **Windows** (`install.ps1`): installs to `%LOCALAPPDATA%\Programs\ConsoleScout` and offers to add it to
  your user `PATH`. Options, after downloading the script: `-Yes`, `-Force`, `-Dir`.
- **Updates.** What the first run chose is kept in `~/.consolescout/install.json`. A later run checks the
  latest release, stops the agent, verifies the download against `SHA256SUMS.txt` and swaps the install
  directory. Your files in `~/.consolescout` are never touched.
- **Uninstalling.** Delete the install directory, the two launchers and the `ConsoleScout` block in your
  rc file (or the `PATH` entry on Windows); `~/.consolescout` holds your keys, settings and history.
- **Releases.** Every push to `master` builds all six platforms (macOS, Linux and Windows, each x64 and
  arm64) in GitHub Actions and publishes a release `v0.1.<run>`.

## Building and publishing

```
dotnet build ConsoleScout.slnx                          # Debug: finds the agent in its own build output
dotnet publish ConsoleScout -c Release -o out           # native CLI + self-contained agent/ + csc
```

The published folder needs no .NET runtime: `ConsoleScout` (about 12 MB, NativeAOT), the `csc` alias,
`appsettings.json`, and `agent/` (self-contained, with Playwright's driver). On macOS the first start of
a freshly published agent can take up to a minute while the system checks its files; `csc -r` waits for
that.

## Project layout

| Project | Contents |
| --- | --- |
| `ConsoleScout` | The CLI (NativeAOT): `App/` commands and DI, `AI/` the assistant loop, prompt, model clients and page condenser, `Tools/` the tools offered to the model, `Agent/` the client for the service, `Configuration/`, `Output/` everything shown in the console, `Donate/`. |
| `ConsoleScout.Agent` | The local web service: Playwright browser host, page fetching and text extraction, search engines. |
| `ConsoleScout.Contracts` | Request and response types shared by both, with source-generated JSON. |

**Adding a tool:** implement `IAssistantTool` (name, description, JSON schema, prompt guidance, invoke)
and register it in `App/ServiceCollectionExtensions.cs`. The assistant loop and the system prompt pick it
up from the registrations.

**Adding a search engine:** implement `ISearchEngine` in the agent and register it in its `Program.cs`;
then add its name to `AgentEngines` in the CLI's `App/ServiceCollectionExtensions.cs`. An engine with
its own API can instead implement `ISearchProvider` in the CLI directly.

## Code style

- One type per file.
- Braces around every `if`/`else`/`for`/`foreach`/`while` body, one-liners included. Enforced by
  `.editorconfig` (IDE0011) as a build warning.
- A method call is not passed as an argument; its result is assigned to a local first. Exceptions:
  string interpolation, LINQ lambdas, fluent chains, property initializers.

## Supporting the developer

Liked the tool? `csc --donate` lists the networks: Bitcoin, Ethereum (ETH, ERC-20), USDT (TRC-20), TON
and Solana. `csc --donate <ticker>`, e.g. `csc --donate btc`, shows that wallet's address
with a QR code. Always check the network before sending: funds sent on the wrong network are lost.

The addresses are constants in `ConsoleScout/Donate/DonationWallets.cs`; a wallet with an empty address
is not shown.
