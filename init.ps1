Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\common\ShowLogo") -Force -DisableNameChecking
Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\common\UI") -Force -DisableNameChecking
Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\local\Init") -Force -DisableNameChecking

################################################
# SCRIPT PURPOSE:
#       1. Install SitecoreDockerTools
#       2. Initialized Wildcard SSL certificate
#       3. Add local host file names
################################################

Show-Logo

################################################
# Ensure License file is in place
################################################

if ( !(Test-Path -Path ".\license\license.xml" -PathType Leaf)) {
    Write-Host "Aborted. Please copy your Sitecore license.xml file into .\license\" -ForegroundColor Yellow
    exit 0
}

$ErrorActionPreference = "Stop";

################################################
# Specify topology folder
################################################

$workingDirectoryPath = ".\docker"

Write-Host
Write-Host "Initializing your Sitecore Containers environment!" -ForegroundColor Green

################################################
# Retrieve and import SitecoreDockerTools module
################################################

Install-SitecoreDockerTools "10.2.7"

##################################
# Configure TLS/HTTPS certificates
##################################

Write-Host
Write-Host "Removing existing certs..." -ForegroundColor Green
Show-Command "Remove-Item -Path `"$PWD\docker\traefik\certs\*.pem`" -Force"
Remove-Item -Path "$PWD\docker\traefik\certs\*.pem" -Force

Push-Location docker\traefik\certs
try {
    $mkcert = ".\mkcert.exe"
    if ($null -ne (Get-Command mkcert.exe -ErrorAction SilentlyContinue)) {
        # mkcert installed in PATH
        $mkcert = "mkcert"
    } elseif (-not (Test-Path $mkcert)) {
        Write-Host "Downloading and installing mkcert certificate tool..." -ForegroundColor Green
		Show-Command "Invoke-WebRequest `"https://github.com/FiloSottile/mkcert/releases/download/v1.4.1/mkcert-v1.4.1-windows-amd64.exe`" -UseBasicParsing -OutFile mkcert.exe"
        Invoke-WebRequest "https://github.com/FiloSottile/mkcert/releases/download/v1.4.1/mkcert-v1.4.1-windows-amd64.exe" -UseBasicParsing -OutFile mkcert.exe
        if ((Get-FileHash mkcert.exe).Hash -ne "1BE92F598145F61CA67DD9F5C687DFEC17953548D013715FF54067B34D7C3246") {
            Remove-Item mkcert.exe -Force
            throw "Invalid mkcert.exe file"
        }
    }
    
    Write-Host
    Write-Host "Generating Traefik TLS certificate..." -ForegroundColor Green
	Show-Command "$mkcert -install"
	Show-Command "$mkcert `"*.sitecorejobs.localhost`""

    Write-Host
    & $mkcert -install
    & $mkcert "*.sitecorejobs.localhost"

    # stash CAROOT path for messaging at the end of the script
    $caRoot = "$(& $mkcert -CAROOT)\rootCA.pem"
}
catch {
    Write-Error "An error occurred while attempting to generate TLS certificate: $_"
}
finally {
    Pop-Location
}


################################
# Add Windows hosts file entries
################################

Initialize-HostNames-ForApp $workingDirectoryPath @()

################################
# Build project images
################################

Invoke-DockerBuild $workingDirectoryPath

################################
# Purge Data Option
################################

Write-Host
$doCleanData = Confirm -Question "Reset local data? This will remove persistent data reverting back to original state."
if($doCleanData)
{
    Write-Host "Reseting local data..." -ForegroundColor Green
    $cmd = Join-Path $workingDirectoryPath 'clean.ps1'
    Show-Command "Invoke-Expression $cmd"
    Invoke-Expression $cmd
}

################################
# Done
################################

Write-Host
Write-Host "Done!" -ForegroundColor Green

Write-Host
Write-Host ("#"*75) -ForegroundColor Cyan
Write-Host "To avoid HTTPS errors, set the NODE_EXTRA_CA_CERTS environment variable" -ForegroundColor Cyan
Write-Host "using the following commmand:" -ForegroundColor Cyan
Write-Host "setx NODE_EXTRA_CA_CERTS $caRoot"
Write-Host
Write-Host "You will need to restart your terminal or VS Code for it to take effect." -ForegroundColor Cyan
Write-Host ("#"*75) -ForegroundColor Cyan
