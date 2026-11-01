# ConsoleScout installer and updater for Windows.
#
#   irm https://raw.githubusercontent.com/fgaliens/ConsoleScout/master/install.ps1 | iex
#
# The first run asks where to install (Enter keeps the default). Later runs update in place without
# asking anything: what was chosen is kept in %USERPROFILE%\.consolescout\install.json, next to the keys.
#
# To pass options, download the script first:  .\install.ps1 -Yes -Force -Dir <path>

param(
    [switch] $Yes,
    [switch] $Force,
    [string] $Dir
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is many times slower with the progress bar

$Repo = 'fgaliens/ConsoleScout'
$DataDir = Join-Path $env:USERPROFILE '.consolescout'
$InfoFile = Join-Path $DataDir 'install.json'
$DefaultDir = Join-Path $env:LOCALAPPDATA 'Programs\ConsoleScout'

# CONSOLESCOUT_RELEASE_URL points the installer at another release server, for testing:
# it serves <url>/latest (the GitHub "latest release" JSON) and <url>/download/<tag>/<asset>.
if ($env:CONSOLESCOUT_RELEASE_URL) {
    $LatestUrl = "$env:CONSOLESCOUT_RELEASE_URL/latest"
    $DownloadUrl = "$env:CONSOLESCOUT_RELEASE_URL/download"
}
else {
    $LatestUrl = "https://api.github.com/repos/$Repo/releases/latest"
    $DownloadUrl = "https://github.com/$Repo/releases/download"
}

function Ask([string] $Prompt, [string] $Default) {
    if ($Yes) { return $Default }
    $reply = Read-Host "$Prompt [$Default]"
    if ([string]::IsNullOrWhiteSpace($reply)) { return $Default }
    return [Environment]::ExpandEnvironmentVariables($reply.Trim())
}

function Confirm([string] $Prompt) {
    if ($Yes) { return $true }
    $reply = Read-Host "$Prompt [Y/n]"
    return -not ($reply -match '^[nN]')
}

function Get-Rid {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    switch ($arch) {
        'X64' { return 'win-x64' }
        'Arm64' { return 'win-arm64' }
        default { throw "Unsupported processor: $arch" }
    }
}

function Stop-Agent {
    $endpoint = Join-Path $DataDir 'agent.json'
    if (-not (Test-Path $endpoint)) { return }

    $agentPid = (Get-Content $endpoint -Raw | ConvertFrom-Json).pid
    $process = Get-Process -Id $agentPid -ErrorAction SilentlyContinue
    if ($null -eq $process) { return }

    # Windows locks the files of a running program, so the agent has to stop before they are replaced.
    Write-Host "Stopping the running agent (pid $agentPid)..."
    Stop-Process -Id $agentPid -Force
    $process.WaitForExit(5000) | Out-Null
}

function Add-ToUserPath([string] $Directory) {
    $current = [Environment]::GetEnvironmentVariable('Path', 'User')
    $entries = @($current -split ';' | Where-Object { $_ })
    if ($entries -contains $Directory) { return $false }

    $updated = (@($entries) + $Directory) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $updated, 'User')
    return $true
}

# --- what is there, and what is new ------------------------------------------------------------------

$rid = Get-Rid
$release = Invoke-RestMethod -Uri $LatestUrl -UseBasicParsing
$tag = $release.tag_name
if (-not $tag) { throw 'No release found.' }
$version = $tag.TrimStart('v')

$updating = Test-Path $InfoFile
if ($updating) {
    $info = Get-Content $InfoFile -Raw | ConvertFrom-Json
    $installDir = $info.installDir
    $shippedSha = $info.appsettingsSha256

    if ($info.version -eq $version -and -not $Force) {
        Write-Host "ConsoleScout $version is already installed in $installDir and up to date."
        return
    }
    Write-Host "Updating ConsoleScout $($info.version) -> $version in $installDir ($rid)."
}
else {
    Write-Host "Installing ConsoleScout $version ($rid)."
    Write-Host 'Press Enter to keep a default.'
    $installDir = if ($Dir) { $Dir } else { Ask 'Install directory' $DefaultDir }
}

# --- download and verify, before touching anything ---------------------------------------------------

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("consolescout-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $asset = "consolescout-$rid.zip"
    $archive = Join-Path $work $asset
    $sums = Join-Path $work 'SHA256SUMS.txt'

    Write-Host "Downloading $asset..."
    Invoke-WebRequest -Uri "$DownloadUrl/$tag/$asset" -OutFile $archive -UseBasicParsing
    Invoke-WebRequest -Uri "$DownloadUrl/$tag/SHA256SUMS.txt" -OutFile $sums -UseBasicParsing

    $expected = (Get-Content $sums | Where-Object { $_ -match " $([regex]::Escape($asset))$" } | ForEach-Object { ($_ -split ' ')[0] })
    $actual = (Get-FileHash $archive -Algorithm SHA256).Hash
    if (-not $expected -or $expected -ne $actual.ToLowerInvariant()) {
        throw "Checksum mismatch for $asset; nothing was changed."
    }

    $new = Join-Path $work 'new'
    Expand-Archive -Path $archive -DestinationPath $new
    if (-not (Test-Path (Join-Path $new 'ConsoleScout.exe'))) { throw 'The archive does not contain ConsoleScout.exe.' }

    # --- keep the user's settings ----------------------------------------------------------------------

    $installedSettings = Join-Path $installDir 'appsettings.json'
    if ($updating -and (Test-Path $installedSettings) -and $shippedSha) {
        $currentSha = (Get-FileHash $installedSettings -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($currentSha -ne $shippedSha) {
            $userSettings = Join-Path $DataDir 'settings.json'
            if (-not (Test-Path $userSettings)) {
                Copy-Item $installedSettings $userSettings
                Write-Host "Your changes to appsettings.json were moved to $userSettings, which updates leave alone."
            }
            else {
                Copy-Item $installedSettings (Join-Path $new 'appsettings.json.bak')
                Write-Host 'appsettings.json had local changes; they are kept as appsettings.json.bak.'
            }
        }
    }

    # --- replace -----------------------------------------------------------------------------------------

    if ($updating) { Stop-Agent }

    $parent = Split-Path $installDir -Parent
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $old = "$installDir.old"
    if (Test-Path $old) { Remove-Item $old -Recurse -Force }
    if (Test-Path $installDir) { Move-Item $installDir $old }
    try {
        Move-Item $new $installDir
    }
    catch {
        if (Test-Path $old) { Move-Item $old $installDir }
        throw "Could not move the new version into $installDir; the previous one was restored. $_"
    }
    if (Test-Path $old) { Remove-Item $old -Recurse -Force }

    $shipped = (Get-FileHash (Join-Path $installDir 'appsettings.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
    [ordered]@{
        version = $version
        rid = $rid
        installDir = $installDir
        binDir = $installDir
        appsettingsSha256 = $shipped
        installedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    } | ConvertTo-Json | Set-Content $InfoFile -Encoding UTF8

    # --- PATH (first install only): csc.cmd and ConsoleScout.exe live in the install directory -----------

    $pathChanged = $false
    if (-not $updating -and (Confirm "Add $installDir to your PATH?")) {
        $pathChanged = Add-ToUserPath $installDir
    }

    # --- start the agent ---------------------------------------------------------------------------------

    # Started now, so the first question does not wait for it: --run-agent also waits for the browser, and
    # downloads one when Chrome is missing. An update restarts the agent it stopped, now in the new version.
    # Not fatal: the agent also starts by itself with the first question that needs it.
    Write-Host ''
    Write-Host 'Starting the background agent (the first start can take a minute)...'
    $exe = Join-Path $installDir 'ConsoleScout.exe'
    try {
        & $exe --run-agent
        $started = $LASTEXITCODE -eq 0
    }
    catch {
        $started = $false
    }
    if (-not $started) {
        Write-Host 'The agent could not be started now; it starts by itself with the first question.'
        Write-Host "Details are in $(Join-Path $DataDir 'agent.log')."
    }

    Write-Host ''
    Write-Host "ConsoleScout $version is installed in $installDir."
    if (-not $updating) {
        Write-Host 'Next steps:'
        Write-Host '  csc --set-key main      save your model''s API key'
        Write-Host '  csc                     ask a question'
        if ($pathChanged) { Write-Host 'Open a new terminal to use csc.' }
    }
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
