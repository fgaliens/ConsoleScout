#!/bin/sh
# ConsoleScout installer and updater for macOS and Linux.
#
#   wget -qO- https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh
#   curl -fsSL https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.sh | sh
#
# The first run asks where to install (Enter keeps the default). Later runs update in place without
# asking anything: what was chosen is kept in ~/.consolescout/install.json, next to the user's keys.
#
# Options: --yes (no questions, defaults), --force (reinstall the same version),
#          --dir <path> (install directory), --bin-dir <path> (directory for the csc command).

set -eu

REPO="fgaliens/ConsoleScout"
DATA_DIR="$HOME/.consolescout"
INFO_FILE="$DATA_DIR/install.json"
DEFAULT_DIR="$HOME/.local/share/consolescout"
DEFAULT_BIN_DIR="$HOME/.local/bin"
RC_BEGIN="# >>> ConsoleScout >>>"
RC_END="# <<< ConsoleScout <<<"

# CONSOLESCOUT_RELEASE_URL points the installer at another release server, for testing:
# it serves <url>/latest (the GitHub "latest release" JSON) and <url>/download/<tag>/<asset>.
if [ -n "${CONSOLESCOUT_RELEASE_URL:-}" ]; then
    LATEST_URL="$CONSOLESCOUT_RELEASE_URL/latest"
    DOWNLOAD_URL="$CONSOLESCOUT_RELEASE_URL/download"
else
    LATEST_URL="https://api.github.com/repos/$REPO/releases/latest"
    DOWNLOAD_URL="https://github.com/$REPO/releases/download"
fi

assume_yes=false
force=false
dir_option=""
bin_option=""

say() { printf '%s\n' "$*"; }
die() { printf 'Error: %s\n' "$*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --yes|-y) assume_yes=true ;;
        --force) force=true ;;
        --dir) [ $# -ge 2 ] || die "--dir needs a path"; dir_option="$2"; shift ;;
        --bin-dir) [ $# -ge 2 ] || die "--bin-dir needs a path"; bin_option="$2"; shift ;;
        -h|--help) sed -n '2,12p' "$0" 2>/dev/null | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) die "unknown option: $1" ;;
    esac
    shift
done

# Questions go to the terminal even when the script itself arrives through a pipe.
can_ask() {
    [ "$assume_yes" = false ] && [ -r /dev/tty ] && [ -w /dev/tty ] && { : </dev/tty; } 2>/dev/null
}

# ask <prompt> <default>: prints the answer, or the default for an empty one.
ask() {
    if can_ask; then
        printf '%s [%s]: ' "$1" "$2" >/dev/tty
        read -r reply </dev/tty || reply=""
        [ -n "$reply" ] && { printf '%s' "$reply"; return; }
    fi
    printf '%s' "$2"
}

# confirm <prompt>: yes unless the user answers n.
confirm() {
    can_ask || return 0
    printf '%s [Y/n]: ' "$1" >/dev/tty
    read -r reply </dev/tty || reply=""
    case "$reply" in [nN]*) return 1 ;; *) return 0 ;; esac
}

# A typed answer such as ~/apps reaches the script with a literal ~, which is expanded here.
# shellcheck disable=SC2088 # the quoted ~ is matched on purpose
expand_home() {
    case "$1" in
        "~") printf '%s' "$HOME" ;;
        "~/"*) printf '%s/%s' "$HOME" "${1#\~/}" ;;
        *) printf '%s' "$1" ;;
    esac
}

fetch() {
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL --retry 3 -o "$2" "$1"
    elif command -v wget >/dev/null 2>&1; then
        wget -q -O "$2" "$1"
    else
        die "neither curl nor wget is installed"
    fi
}

sha256() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | cut -d' ' -f1
    else
        shasum -a 256 "$1" | cut -d' ' -f1
    fi
}

# json_value <file> <key>: a string or number value from the flat JSON files this script deals with.
json_value() {
    sed -n "s/.*\"$2\"[[:space:]]*:[[:space:]]*\"\{0,1\}\([^\",}]*\)\"\{0,1\}.*/\1/p" "$1" | head -n 1
}

detect_rid() {
    os=$(uname -s)
    arch=$(uname -m)

    case "$os" in
        Darwin)
            # Under Rosetta uname reports x86_64 even on Apple silicon; the native build is better.
            if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || echo 0)" = "1" ]; then
                arch=arm64
            fi
            platform=osx
            ;;
        Linux)
            if ldd --version 2>&1 | grep -qi musl; then
                die "musl-based Linux (e.g. Alpine) is not supported: the builds need glibc 2.39 or newer"
            fi
            platform=linux
            ;;
        *) die "unsupported system: $os (on Windows use install.ps1)" ;;
    esac

    case "$arch" in
        x86_64|amd64) arch=x64 ;;
        arm64|aarch64) arch=arm64 ;;
        *) die "unsupported processor: $arch" ;;
    esac

    printf '%s-%s' "$platform" "$arch"
}

# The rc file of the user's shell, for the PATH line and the zsh alias.
rc_file() {
    case "${SHELL:-}" in
        */zsh) printf '%s' "${ZDOTDIR:-$HOME}/.zshrc" ;;
        */bash)
            if [ "$(uname -s)" = Darwin ]; then printf '%s' "$HOME/.bash_profile"; else printf '%s' "$HOME/.bashrc"; fi
            ;;
        */fish) printf '%s' "$HOME/.config/fish/config.fish" ;;
        *) printf '%s' "$HOME/.profile" ;;
    esac
}

# write_rc_block <rc> <content>: replaces the ConsoleScout block of the rc file, or appends one.
write_rc_block() {
    rc="$1"
    mkdir -p "$(dirname "$rc")"
    touch "$rc"
    kept=$(mktemp)
    sed "/^$RC_BEGIN\$/,/^$RC_END\$/d" "$rc" >"$kept"
    {
        cat "$kept"
        printf '%s\n%s\n%s\n' "$RC_BEGIN" "$2" "$RC_END"
    } >"$rc"
    rm -f "$kept"
}

stop_agent() {
    endpoint="$DATA_DIR/agent.json"
    [ -f "$endpoint" ] || return 0
    pid=$(json_value "$endpoint" pid)
    if [ -z "$pid" ] || ! kill -0 "$pid" 2>/dev/null; then
        return 0
    fi

    say "Stopping the running agent (pid $pid)..."
    kill "$pid" 2>/dev/null || true
    waited=0
    while kill -0 "$pid" 2>/dev/null && [ "$waited" -lt 10 ]; do
        sleep 0.5
        waited=$((waited + 1))
    done
}

write_info() {
    mkdir -p "$DATA_DIR"
    cat >"$INFO_FILE" <<EOF
{
  "version": "$1",
  "rid": "$2",
  "installDir": "$3",
  "binDir": "$4",
  "appsettingsSha256": "$5",
  "installedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
EOF
}

# --- what is there, and what is new ----------------------------------------------------------------

rid=$(detect_rid)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT INT TERM

fetch "$LATEST_URL" "$work/latest.json" || die "cannot reach the release server ($LATEST_URL)"
tag=$(json_value "$work/latest.json" tag_name)
[ -n "$tag" ] || die "no release found"
version="${tag#v}"

updating=false
if [ -f "$INFO_FILE" ]; then
    updating=true
    install_dir=$(json_value "$INFO_FILE" installDir)
    bin_dir=$(json_value "$INFO_FILE" binDir)
    installed=$(json_value "$INFO_FILE" version)
    shipped_sha=$(json_value "$INFO_FILE" appsettingsSha256)

    if [ "$installed" = "$version" ] && [ "$force" = false ]; then
        say "ConsoleScout $version is already installed in $install_dir and up to date."
        exit 0
    fi
    say "Updating ConsoleScout $installed -> $version in $install_dir ($rid)."
else
    say "Installing ConsoleScout $version ($rid)."
    say "Press Enter to keep a default."
    install_dir=$(expand_home "${dir_option:-$(ask "Install directory" "$DEFAULT_DIR")}")
    bin_dir=$(expand_home "${bin_option:-$(ask "Directory for the csc command" "$DEFAULT_BIN_DIR")}")
fi

if [ -z "$install_dir" ] || [ -z "$bin_dir" ]; then
    die "the install directory and the command directory must not be empty"
fi

# --- download and verify, before touching anything ---------------------------------------------------

asset="consolescout-$rid.tar.gz"
say "Downloading $asset..."
fetch "$DOWNLOAD_URL/$tag/$asset" "$work/$asset" || die "cannot download $asset"
fetch "$DOWNLOAD_URL/$tag/SHA256SUMS.txt" "$work/SHA256SUMS.txt" || die "cannot download SHA256SUMS.txt"

expected=$(grep " $asset\$" "$work/SHA256SUMS.txt" | cut -d' ' -f1)
actual=$(sha256 "$work/$asset")
if [ -z "$expected" ] || [ "$expected" != "$actual" ]; then
    die "checksum mismatch for $asset; nothing was changed"
fi

mkdir -p "$work/new"
tar -xzf "$work/$asset" -C "$work/new"
[ -x "$work/new/ConsoleScout" ] || die "the archive does not contain ConsoleScout"

# --- keep the user's settings ------------------------------------------------------------------------

if [ "$updating" = true ] && [ -f "$install_dir/appsettings.json" ]; then
    current_sha=$(sha256 "$install_dir/appsettings.json")
    if [ -n "$shipped_sha" ] && [ "$current_sha" != "$shipped_sha" ]; then
        if [ ! -f "$DATA_DIR/settings.json" ]; then
            cp "$install_dir/appsettings.json" "$DATA_DIR/settings.json"
            say "Your changes to appsettings.json were moved to $DATA_DIR/settings.json, which updates leave alone."
        else
            cp "$install_dir/appsettings.json" "$work/new/appsettings.json.bak"
            say "appsettings.json had local changes; they are kept as appsettings.json.bak."
            say "Put lasting changes in $DATA_DIR/settings.json instead."
        fi
    fi
fi

# --- replace -------------------------------------------------------------------------------------------

[ "$updating" = true ] && stop_agent

mkdir -p "$(dirname "$install_dir")"
rm -rf "$install_dir.new" "$install_dir.old"
mv "$work/new" "$install_dir.new"
if [ -e "$install_dir" ]; then
    mv "$install_dir" "$install_dir.old"
fi
if ! mv "$install_dir.new" "$install_dir"; then
    [ -e "$install_dir.old" ] && mv "$install_dir.old" "$install_dir"
    die "could not move the new version into $install_dir; the previous one was restored"
fi
rm -rf "$install_dir.old"

# Small scripts rather than symlinks: they always run the binary from its own directory, where it finds
# appsettings.json and the agent, however the system resolves a symlinked executable.
mkdir -p "$bin_dir"
for name in csc ConsoleScout; do
    printf '#!/bin/sh\nexec "%s/ConsoleScout" "$@"\n' "$install_dir" >"$bin_dir/$name"
    chmod +x "$bin_dir/$name"
done

shipped_sha=$(sha256 "$install_dir/appsettings.json")
write_info "$version" "$rid" "$install_dir" "$bin_dir" "$shipped_sha"

# --- PATH and the zsh alias (first install only) --------------------------------------------------------

path_changed=false
if [ "$updating" = false ]; then
    rc=$(rc_file)
    block=""

    case ":$PATH:" in
        *":$bin_dir:"*) ;;
        *)
            if confirm "Add $bin_dir to PATH in $rc?"; then
                case "$rc" in
                    *.fish) block="fish_add_path \"$bin_dir\"" ;;
                    *) block="export PATH=\"$bin_dir:\$PATH\"" ;;
                esac
                path_changed=true
            fi
            ;;
    esac

    case "$rc" in
        *.zshrc)
            if confirm "Add alias csc='noglob csc', so that '?' in questions works in zsh?"; then
                alias_line="alias csc='noglob csc'"
                block="${block:+$block
}$alias_line"
                path_changed=true
            fi
            ;;
    esac

    [ -n "$block" ] && write_rc_block "$rc" "$block"
fi

# --- start the agent -------------------------------------------------------------------------------------

# Started now, so the first question does not wait for it: --run-agent also waits for the browser, and
# downloads one when Chrome is missing. An update restarts the agent it stopped, now in the new version.
# Not fatal: the agent also starts by itself with the first question that needs it.
say ""
say "Starting the background agent (the first start can take a minute)..."
if ! "$install_dir/ConsoleScout" --run-agent </dev/null; then
    say "The agent could not be started now; it starts by itself with the first question."
    say "Details are in $DATA_DIR/agent.log."
fi

# --- done ----------------------------------------------------------------------------------------------

say ""
say "ConsoleScout $version is installed in $install_dir."
if [ "$updating" = false ]; then
    say "Next steps:"
    say "  csc --set-key main      save your model's API key"
    say "  csc                     ask a question"
    if [ "$path_changed" = true ]; then
        say "Open a new terminal (or run: . $(rc_file)) to use csc."
    fi
fi
