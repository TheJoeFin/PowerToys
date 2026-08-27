# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
    Registers the Workspaces Tab Sync native messaging host for local (developer-mode) testing.

.DESCRIPTION
    Builds a concrete host manifest from the template (filling in the absolute exe path and the
    extension's id) and points the per-user browser registry key at it. No admin rights needed.

.PARAMETER ExtensionId
    The id the browser assigned to the unpacked extension (see edge://extensions).

.PARAMETER Browser
    Which browser(s) to register for: Edge, Chrome, or Both. Defaults to Edge.

.PARAMETER ExePath
    Path to PowerToys.WorkspacesBrowserSync.exe. Defaults to the Debug build output.

.EXAMPLE
    .\register-dev.ps1 -ExtensionId abcdefghijklmnopabcdefghijklmnop -Browser Both
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ExtensionId,

    [ValidateSet('Edge', 'Chrome', 'Both')]
    [string]$Browser = 'Edge',

    [string]$ExePath
)

$ErrorActionPreference = 'Stop'
$hostName = 'com.microsoft.powertoys.workspaces'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $ExePath) {
    # Find the built exe under bin\ regardless of platform/config (x64, ARM64, Debug, Release),
    # preferring the most recently built one.
    $found = Get-ChildItem -Path (Join-Path $scriptDir 'bin') -Recurse -Filter 'PowerToys.WorkspacesBrowserSync.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($found) {
        $ExePath = $found.FullName
    }
}

if (-not $ExePath -or -not (Test-Path $ExePath)) {
    throw "Host exe not found under '$scriptDir\bin'. Build it first: dotnet build PowerToys.WorkspacesBrowserSync.csproj -c Debug -p:Platform=ARM64"
}

$ExePath = (Resolve-Path $ExePath).Path

# Materialize a concrete manifest next to the exe.
$template = Get-Content (Join-Path $scriptDir 'com.microsoft.powertoys.workspaces.template.json') -Raw
$manifest = $template `
    -replace 'REPLACED_WITH_ABSOLUTE_EXE_PATH', ($ExePath -replace '\\', '\\') `
    -replace 'REPLACED_WITH_EXTENSION_ID', $ExtensionId

$manifestPath = Join-Path $scriptDir "$hostName.json"
Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8
Write-Host "Wrote manifest: $manifestPath"

function Register-ForBrowser([string]$registryRoot, [string]$label) {
    $key = "$registryRoot\NativeMessagingHosts\$hostName"
    New-Item -Path $key -Force | Out-Null
    Set-ItemProperty -Path $key -Name '(default)' -Value $manifestPath
    Write-Host "Registered for ${label}: $key"
}

if ($Browser -in @('Edge', 'Both')) {
    Register-ForBrowser 'HKCU:\SOFTWARE\Microsoft\Edge' 'Edge'
}

if ($Browser -in @('Chrome', 'Both')) {
    Register-ForBrowser 'HKCU:\SOFTWARE\Google\Chrome' 'Chrome'
}

Write-Host "`nDone. Restart the browser, then click 'Sync URLs' in the extension popup."
