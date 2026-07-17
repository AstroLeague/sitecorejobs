Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\common\ShowLogo") -Force -DisableNameChecking
Import-Module -Name (Join-Path $PSScriptRoot "docker\tools\common\UI") -Force -DisableNameChecking

################################################
# SCRIPT PURPOSE:
#       1. Compose down docker containers
#       2. Prune docker system
################################################

Show-Logo

################################################
# Specify topology folder
################################################

$workingDirectoryPath = ".\docker"

################################################
# Compose Down
################################################

Write-Host "Down containers..." -ForegroundColor Green

Show-Command "cd $workingDirectoryPath"
Push-Location $workingDirectoryPath

try {
	
  Show-Command "docker compose down"
  docker compose down
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Container down failed, see errors above."
  }
  
  ################################################
  # Prune
  ################################################
  Write-Host
  Write-Host "Docker system prune..." -ForegroundColor Yellow
  Show-Command "docker system prune -f"
  docker system prune -f
}
finally {
  Pop-Location
}
