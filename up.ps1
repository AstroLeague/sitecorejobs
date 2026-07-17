Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\common\ShowLogo") -Force -DisableNameChecking
Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\common\UI") -Force -DisableNameChecking
Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\local\Up") -Force -DisableNameChecking
Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\local\Init") -Force -DisableNameChecking

Show-Logo
Write-Host "Starting Sitecore Jobs local environment..." -ForegroundColor Green

if (!(Test-Path -Path ".\license\license.xml" -PathType Leaf)) {
    Write-Host
    Write-Host "Aborted. Please copy your Sitecore license.xml file into .\license\" -ForegroundColor Yellow
    exit 0
}

$ErrorActionPreference = "Stop"
$workingDirectoryPath = ".\docker"

Start-Docker $workingDirectoryPath
Init-SitecoreCli "5.1.25"

Write-Host
$isFirstRun = Confirm -Question "Init Solr Indexes? (first time only)"
if ($isFirstRun) {
    Write-Host "Configuring Sitecore indexes..." -ForegroundColor Green
    $indexNames = @(
        'sitecore_core_index',
        'sitecore_master_index',
        'sitecore_web_index',
        'sitecore_sxa_master_index',
        'sitecore_sxa_web_index'
    )
    Init-SitecoreIndexes $workingDirectoryPath $indexNames
}

Write-Host
Write-Host "Opening Sitecore..." -ForegroundColor Green
Start-Process https://cm.sitecorejobs.localhost/sitecore/
