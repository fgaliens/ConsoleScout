# ConsoleScout

A question-answering assistant for the terminal. Ask in plain words; the answer appears right in the
console. When the question depends on something current, it searches the web and reads the relevant
pages first.

```
$ csc when was .NET 10 released
```

It knows the current date and time and your operating system, so answers about "today" and commands
for your shell come out right. It works with any OpenAI-compatible chat API, such as OpenRouter or
OpenAI.

## Installation

### macOS

Open Terminal and run:

```
curl -fsSL https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh
```

### Linux

```
wget -qO- https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh
```

or, if your system has `curl` instead of `wget`:

```
curl -fsSL https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh
```

Requires glibc 2.39 or newer: Ubuntu 24.04, Debian 13, Fedora 40 or later. Alpine is not supported.

### Windows

Open PowerShell and run:

```
irm https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.ps1 | iex
```

### What the installer does

It picks the right build for your system and processor (Intel or ARM, Apple silicon included), asks
two questions and adds `csc` to your `PATH`. Press Enter to keep a default:

| Question | Default on macOS and Linux | Default on Windows |
| --- | --- | --- |
| Install directory | `~/.local/share/consolescout` | `%LOCALAPPDATA%\Programs\ConsoleScout` |
| Directory for the `csc` command | `~/.local/bin` | the install directory |

On macOS and Linux it then offers to add that directory to your shell's `PATH` and, in zsh, to make `?`
work in questions (see [Troubleshooting](#troubleshooting)). Open a new terminal afterwards.

Finally it starts the background service that searches the web and keeps your history, so the first
question is answered without delay. Web search uses Google Chrome when it is installed; otherwise the
installer downloads a browser at this step, which can take a minute.

### Updating

Run the same command again. It updates in place without asking anything, keeps your keys, settings
and history, and restarts the background service in the new version.

### Installing without questions

Add `--yes` to accept every default, or choose the directories directly:

```
curl -fsSL https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh -s -- --yes
curl -fsSL https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh -s -- --dir ~/apps/consolescout --bin-dir ~/bin
```

On Windows, download the script first and pass `-Yes` or `-Dir <path>`:

```
irm https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.ps1 -OutFile install.ps1
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Yes
```

`--force` (`-Force`) reinstalls the current version.

### Uninstalling

- macOS and Linux: delete the install directory, `csc` and `ConsoleScout` in the command directory, and
  the block between `# >>> ConsoleScout >>>` and `# <<< ConsoleScout <<<` in your shell's rc file
  (`~/.zshrc`, `~/.bashrc` or similar).
- Windows: delete the install directory and remove it from your user `PATH`
  (Settings → System → About → Advanced system settings → Environment Variables).

Your keys, settings and history stay in `~/.consolescout` (`%USERPROFILE%\.consolescout` on Windows);
delete it too to remove everything.

## Getting started

1. **Choose your models.** Create `~/.consolescout/settings.json` with your provider's address and model
   names. For example, with [OpenRouter](https://openrouter.ai):

   ```json
   {
     "models": {
       "main":       { "endpoint": "https://openrouter.ai/api/v1", "name": "openai/gpt-6.1-terra" },
       "summarizer": { "endpoint": "https://openrouter.ai/api/v1", "name": "anthropic/claude-haiku-5.5" }
     }
   }
   ```

   `main` answers your questions. `summarizer` is a cheaper model that shortens the web pages `main` reads,
   which saves money and makes answers faster. To use only one model, add
   `"tools": { "webFetch": { "condense": false } }` and skip its key below.

2. **Save the API keys.** Each command asks for the key without showing it on screen:

   ```
   csc --set-key main
   csc --set-key summarizer
   ```

   With OpenRouter, both models use the same key.

3. **Ask.**

   ```
   csc what is the latest version of Node.js
   ```

`csc --status` shows whether everything is set up.

## Usage

`csc` and `ConsoleScout` are the same command.

| Command | What it does |
| --- | --- |
| `csc <question>` | Answers the question. |
| `csc` | Asks for a question. While a key is missing, shows what to set up instead. |
| `csc -l <question>` | A detailed answer (`--long`). |
| `csc -s <question>` | The shortest answer (`--short`). |
| `csc -c <question>` | Follows up on your previous answer (`--continue`); `-c d7` continues dialog 7. See [Follow-up questions](#follow-up-questions). |
| `csc -d` | Turns dialog mode on or off in this terminal (`--dialog`): every question continues the conversation. See [Dialog mode](#dialog-mode). |
| `csc -n <question>` | Starts a new dialog, even in dialog mode (`--new`). |
| `csc --set-key main` | Saves an API key (`-k`). Models: `main`, `summarizer`. `-k main=<key>` takes the key from the command line instead of asking. |
| `csc --status` | Shows the setup: settings, keys, the background service, the terminal session. |
| `csc --readme` | Shows this documentation. |
| `csc -r` | Starts the background service now (`--run-agent`), so the first web question is fast. |
| `csc --donate` | Shows how to support the developer. |
| `csc --no-donate` | Hides the donation hint shown while the model thinks. |
| `csc -h` | Lists all options. |

A question cannot be combined with `--set-key`, `--status`, `--readme`, `-r`, `--donate` or `--no-donate`.

## Follow-up questions

Under every answer you see two numbers: the message, `x42`, and its dialog, `d40` (the number of the
dialog's first message). With `-c` the model sees the conversation so far, so you can build on it:

```
csc how do I list open ports on macOS            # Message x40 · Dialog d40
csc -c and only the ones listening on IPv6       # follows up on the last answer in this terminal
csc -c d40 and on Linux                          # continues dialog 40, from any terminal
csc -c x40 what about Windows                    # branches off message 40, from any terminal
csc -c n1 and with sudo                          # follows up on the answer before the last one
```

- `-c` (also `--continue`) and `-c n<number>` look at the questions asked in the same terminal window or
  tab; each tab has its own history. `d<number>` and `x<number>` work from anywhere.
- `d<number>` continues a dialog where it stopped, at its latest message. `x<number>` continues from that
  exact message, which branches the dialog when the message is not its latest.
- Only `x…`, `d…` and `n…` right after `-c` count as a reference: `csc -c 3 apples` asks about "3 apples".
- The model sees up to 5 earlier questions of the conversation. The history is kept for 30 days.

### Dialog mode

In dialog mode every question in the terminal tab continues the conversation, without `-c`:

```
csc -d                       # dialog mode on: the next question starts a dialog
csc what is a hash table
csc and how is it different from a tree     # continues, no -c needed
csc -n how do I bake bread                  # starts a new dialog, the mode stays on
csc -d                       # dialog mode off
```

- `csc -c -d` turns the mode on for the conversation you have been continuing with `-c`; `csc -c d40 -d`
  or `csc -c x42 -d` for that dialog or message.
- With a question, `-d` turns the mode on and asks right away: `csc -d tell me more`, or
  `csc -c -d tell me more` to continue the current conversation. It never turns the mode off; only
  `csc -d` alone does.
- While the mode is on, the line under each answer says so, with how to start a new dialog (`csc -n`) and
  how to turn the mode off (`csc -d`). `csc --status` shows it too.
- The mode belongs to the terminal tab: other tabs are not affected.

## How answers are made

- **Web search.** The model searches when the answer depends on something current: news, prices,
  versions, weather. It uses DuckDuckGo, and Google when DuckDuckGo turns the search away.
- **Reading pages.** It reads the most relevant results in full, several at once, before answering.
- **No search when it is not needed.** General knowledge, writing and coding questions are answered
  directly.
- **Language.** Answers come in the language you asked in.

## Configuration

Your settings go in `~/.consolescout/settings.json` (`%USERPROFILE%\.consolescout\settings.json` on
Windows). Updates never change this file. Everything is optional:

```json
{
  "models": {
    "main":       { "endpoint": "https://openrouter.ai/api/v1", "name": "openai/gpt-6.1-terra" },
    "summarizer": { "endpoint": "https://openrouter.ai/api/v1", "name": "anthropic/claude-haiku-5.5" }
  },
  "tools": {
    "webSearch": { "enabled": true, "providers": [ "duckduckgo", "google" ], "maxResults": 5 },
    "webFetch":  { "enabled": true, "condense": true }
  },
  "preferences": { "answerLength": "normal" },
  "history":     { "enabled": true, "maxContextTurns": 5 },
  "output":      { "format": "markdown" }
}
```

| Setting | Meaning |
| --- | --- |
| `models.*.endpoint`, `models.*.name` | Your provider's API address and the model to use. |
| `tools.webSearch.enabled`, `tools.webFetch.enabled` | Turn web search or page reading off. With both off, answers come from the model's own knowledge. |
| `tools.webSearch.providers` | Search engines, tried in order: `duckduckgo`, `google`. |
| `tools.webFetch.condense` | Let the `summarizer` shorten pages; `false` sends them to `main` as they are. |
| `preferences.answerLength` | Default length: `normal`, `short` or `long`. |
| `history.enabled` | `false` stops saving answers; `-c` is then unavailable. |
| `output.format` | `markdown` (formatted) or `plain` text. |

A misspelled setting is reported as an error rather than ignored.

**Environment variables** override any setting: `CONSOLESCOUT_` followed by the path with `__` between
parts, for example `CONSOLESCOUT_MODELS__MAIN__NAME=openai/gpt-6.1-sol`.

**API keys** are stored by `csc --set-key` in `~/.consolescout/secrets.json`, readable only by you. You
can also set them as `CONSOLESCOUT_MODELS__MAIN__APIKEY`.

## Troubleshooting

- **`zsh: no matches found` with `?` in a question.** zsh reads `?` as a file pattern before `csc` even
  starts. Let the installer add `alias csc='noglob csc'` to `~/.zshrc` (or add it yourself), or put the
  question in quotes: `csc "what is new in .NET 10?"`. Typing the question after running plain `csc`
  works too.
- **`csc` runs the C# compiler instead.** Mono and Visual Studio's Developer Command Prompt also provide
  a `csc` command. Use `ConsoleScout` instead, or put the ConsoleScout directory earlier in your `PATH`.
- **The first web question takes a few seconds.** A background service with a browser starts on the
  first question and stays up between questions. Run `csc -r` once to start it ahead of time.
- **"No search engine answered".** Search engines sometimes answer automated searches with a CAPTCHA.
  ConsoleScout then tries the next engine in `tools.webSearch.providers`; when all of them refuse, the
  answer comes from the model's own knowledge with a warning. Trying again later usually helps.
- **macOS 13 (Ventura).** Install Google Chrome: the browser that would otherwise be downloaded no longer
  supports this version.
- **Something else.** `csc --status` shows what is set up and what is missing. The background service
  logs to `~/.consolescout/agent.log`.

## Privacy

- Your questions go to the model provider you configured. Search queries go to the search engines, and
  the pages the model reads are downloaded from their sites. The installer contacts GitHub. Nothing is
  sent anywhere else.
- Keys, settings and history are stored only on your computer, in `~/.consolescout`. The key and history
  files are readable only by you.
- The background service accepts connections only from your own computer, and only with a secret that
  changes on every start.

## Supporting the developer

Like the tool? `csc --donate` lists the networks you can donate on: Bitcoin, Ethereum, USDT (TRC-20), TON
and Solana. `csc --donate btc` (or `eth`, `usdt`, `ton`, `sol`) shows the address and a QR code to scan.
Always check the network before sending: funds sent on the wrong network are lost.

## Contributing

Building from source and how the project is organised: see [CONTRIBUTING.md](CONTRIBUTING.md).
