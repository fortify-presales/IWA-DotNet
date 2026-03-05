#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publish and deploy DotNET WebApp to Azure App Service.

.DESCRIPTION
    Runs dotnet publish, optionally packages the publish output as a ZIP,
    and deploys using az webapp deploy. Reads defaults from deploy.config
    (JSON or key=value) and allows CLI parameters to override.

.PARAMETER Configuration
    Build configuration to publish (Debug or Release). Default: Debug

.PARAMETER Project
    Path to the .csproj to publish. Default: .\InsecureWebApp\InsecureWebApp.csproj

.PARAMETER Output
    Output folder for publish. Default: .\publish

.PARAMETER ResourceGroup
    Azure resource group containing the App Service.

.PARAMETER AppName
    Azure App Service name.

.PARAMETER ZipDeploy
    If specified, create a ZIP from the publish output and deploy the ZIP.
    Otherwise deploy the folder (az webapp deploy will zip+extract on server).

.PARAMETER Login
    If specified, run az login before deploy when not already logged in.

.PARAMETER DryRun
    If specified, echo the commands that would run without executing them.

.EXAMPLE
    .\deploy.ps1

.EXAMPLE
    .\deploy.ps1 -Configuration Release -ZipDeploy -ResourceGroup rg-iwa-dev-uks-001 -AppName iwadotnet

#>

[CmdletBinding()]
param(
    [string] $Configuration = 'Debug',
    [string] $Project = '.\InsecureWebApp\InsecureWebApp.csproj',
    [string] $Output = '.\publish',
    [string] $ResourceGroup = 'rg-iwa-dev-uks-001',
    [string] $AppName = 'iwadotnet',
    [switch] $ZipDeploy,
    [switch] $Login,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host $msg -ForegroundColor Green }
function Write-ErrorAndExit($msg) { Write-Host $msg -ForegroundColor Red; exit 1 }

function Convert-ToBool($v) {
    if ($null -eq $v) { return $false }
    switch ($v.ToString().ToLowerInvariant()) {
        '1' { return $true }
        'true' { return $true }
        'yes' { return $true }
        default { return $false }
    }
}

# Load deploy.config (JSON or key=value) unless parameters supplied via CLI
function Load-DeployConfig {
    param([string]$Path)
    if (-not (Test-Path -Path $Path)) { return $null }
    try {
        $raw = Get-Content -Raw -Path $Path
        $trim = $raw.TrimStart()
        if ($trim.StartsWith('{')) {
            return $raw | ConvertFrom-Json
        } else {
            $obj = @{}
            $lines = $raw -split "`n"
            foreach ($line in $lines) {
                $l = $line.Trim()
                if ([string]::IsNullOrWhiteSpace($l)) { continue }
                if ($l -match '^\s*[#;]') { continue }
                if ($l -match '^(.*?)=(.*)$') {
                    $k = $matches[1].Trim()
                    $v = $matches[2].Trim()
                    # remove surrounding quotes if present
                    if ($v.StartsWith('"') -and $v.EndsWith('"')) { $v = $v.Trim('"') }
                    if ($v.StartsWith("'") -and $v.EndsWith("'")) { $v = $v.Trim("'") }
                    $obj[$k] = $v
                }
            }
            return $obj
        }
    } catch {
        Write-Info "Failed to parse deploy.config: $($_.Exception.Message)"
        return $null
    }
}

# Determine config path (script directory or current dir)
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$configPath = Join-Path $scriptDir 'deploy.config'
$config = Load-DeployConfig -Path $configPath
if ($config) {
    Write-Info "Loaded deploy.config from $configPath"
    # Merge config values when parameter not explicitly provided
    if (-not $PSBoundParameters.ContainsKey('Configuration') -and $config.Configuration) { $Configuration = $config.Configuration }
    if (-not $PSBoundParameters.ContainsKey('Project') -and $config.Project) { $Project = $config.Project }
    if (-not $PSBoundParameters.ContainsKey('Output') -and $config.Output) { $Output = $config.Output }
    if (-not $PSBoundParameters.ContainsKey('ResourceGroup') -and $config.ResourceGroup) { $ResourceGroup = $config.ResourceGroup }
    if (-not $PSBoundParameters.ContainsKey('AppName') -and $config.AppName) { $AppName = $config.AppName }

    if (-not $PSBoundParameters.ContainsKey('ZipDeploy') -and $config.ZipDeploy) {
        $ZipDeploy = [bool](Convert-ToBool $config.ZipDeploy)
    }
    if (-not $PSBoundParameters.ContainsKey('Login') -and $config.Login) {
        $Login = [bool](Convert-ToBool $config.Login)
    }
    if (-not $PSBoundParameters.ContainsKey('DryRun') -and $config.DryRun) {
        $DryRun = [bool](Convert-ToBool $config.DryRun)
    }
}

try {
    Write-Info "Parameters:"
    Write-Info "  Configuration: $Configuration"
    Write-Info "  Project: $Project"
    Write-Info "  Output: $Output"
    Write-Info "  ResourceGroup: $ResourceGroup"
    Write-Info "  AppName: $AppName"
    Write-Info "  ZipDeploy: $($ZipDeploy.IsPresent)"
    Write-Info "  DryRun: $($DryRun.IsPresent)"
    Write-Info ""

    # Ensure project exists
    if (-not (Test-Path -Path $Project)) {
        Write-ErrorAndExit "Project file not found: $Project"
    }

    # Ensure output folder is an absolute/normalized path
    $pubFolder = Resolve-Path -LiteralPath $Output -ErrorAction SilentlyContinue
    if ($null -eq $pubFolder) {
        $pubFolder = Join-Path (Get-Location) $Output
    } else {
        $pubFolder = $pubFolder.Path
    }

    # Run dotnet publish
    $publishCmd = "dotnet publish `"$Project`" -c $Configuration -o `"$pubFolder`" /p:CreatePackageOnPublish=false"
    if ($DryRun) {
        Write-Info "[DryRun] $publishCmd"
    } else {
        Write-Info "Running: $publishCmd"
        dotnet publish $Project -c $Configuration -o $pubFolder /p:CreatePackageOnPublish=false
        Write-Success "dotnet publish completed. Output: $pubFolder"
    }

    # Determine src-path for az deploy
    if ($ZipDeploy) {
        $zipPath = Join-Path $pubFolder 'publish.zip'
        if ($DryRun) {
            Write-Info "[DryRun] Compressing $pubFolder -> $zipPath"
        } else {
            if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
            Write-Info "Creating ZIP: $zipPath"
            Compress-Archive -Path (Join-Path $pubFolder '*') -DestinationPath $zipPath -Force
            Write-Success "ZIP created: $zipPath"
        }
        $srcPath = $zipPath
    } else {
        $srcPath = $pubFolder
    }

    # Ensure az logged in (if requested or not logged in)
    $needLogin = $false
    try {
        az account show --only-show-errors > $null 2>&1
    } catch {
        $needLogin = $true
    }

    if ($Login -or $needLogin) {
        if ($DryRun) {
            Write-Info "[DryRun] az login"
        } else {
            Write-Info "Logging into Azure (interactive)..."
            az login --only-show-errors
        }
    }

    # Validate src path exists
    if (-not (Test-Path -Path $srcPath)) {
        Write-ErrorAndExit "Deployment source not found: $srcPath"
    }

    # Run az webapp deploy
    if ($ZipDeploy) {
        $deployCmd = "az webapp deploy --resource-group `"$ResourceGroup`" --name `"$AppName`" --src-path `"$srcPath`" --type zip --restart true --track-status"
    } else {
        $deployCmd = "az webapp deploy --resource-group `"$ResourceGroup`" --name `"$AppName`" --src-path `"$srcPath`" --restart true --track-status"
    }

    if ($DryRun) {
        Write-Info "[DryRun] $deployCmd"
        Write-Info "Dry run completed."
    } else {
        Write-Info "Starting deployment..."
        if ($ZipDeploy) {
            az webapp deploy --resource-group $ResourceGroup --name $AppName --src-path $srcPath --type zip --restart true --track-status
        } else {
            az webapp deploy --resource-group $ResourceGroup --name $AppName --src-path $srcPath --restart true --track-status
        }
        Write-Success "Deployment finished successfully."
    }

} catch {
    Write-Host "An error occurred: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}