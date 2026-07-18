Import-Module -Name (Join-Path $PSScriptRoot "..\common\UI") -Force -DisableNameChecking

Set-StrictMode -Version Latest

function Start-Docker {
    param(
        [ValidateNotNullOrEmpty()]
        [string] 
        $DockerRoot = ".\docker"
    )

	Initialize-DockerBindMounts $DockerRoot
	
	Write-Host
	Write-Host "Entering $DockerRoot" -ForegroundColor Green
	Show-Command "cd $DockerRoot"
	Push-Location $DockerRoot

	try {
		# PREP
		Write-Host
		Write-Host "PREP: Clearing obstacles..." -ForegroundColor Green

		# STOP local iis service before running docker
		Write-Host
		Write-Host "[iis] Stopping your iis service in order to run docker" -ForegroundColor Green
		Show-Command "iisreset /stop"
		iisreset /stop

		# STOP local solr services before running docker
		Write-Host
		Write-Host "[solr] MANUAL - Please ensure solr is not running locally" -ForegroundColor Green
		# Write-Host "[solr] Scanning any solr or nssm services to stop them in order to run docker" -ForegroundColor Green
			
		Write-Host
		Write-Host "[license] Clearing Sitecore environment license variable to avoid issues..." -ForegroundColor Green
		Show-Command '$Env:Sitecore_License = ""'
		$Env:Sitecore_License = ""

		# Start the Sitecore instance
		Write-Host
		Write-Host "Starting Sitecore environment..." -ForegroundColor Green
		Show-Command "docker compose up -d --remove-orphans"
		docker compose up -d --remove-orphans
		if ($LASTEXITCODE -ne 0) {
			throw "Docker compose up failed. See errors above."
		}
	
    } finally {
		Pop-Location
    }

	Write-Host
	Write-Host "Deploying latest code..." -ForegroundColor Green
	$msbuildPath = Resolve-MSBuildPath
	Write-Host "Using MSBuild: $msbuildPath" -ForegroundColor Green
	Show-Command @"
& "$msbuildPath" .\src\Build\HelixPublishingPipeline\HPP.Platform\HPP.Platform.csproj `
		 /maxCpuCount `
		 /p:Configuration=debug `
		 /p:DeployOnBuild=true `
		 /p:PublishProfile=Local `
		 /nologo `
		 /detailedSummary:False `
		 /verbosity:quiet `
		 /clp:ErrorsOnly
"@
	& $msbuildPath .\src\Build\HelixPublishingPipeline\HPP.Platform\HPP.Platform.csproj `
	 /p:Configuration=debug `
	 /m `
	 /p:DeployOnBuild=true `
	 /p:PublishProfile=Local `
	 /nologo `
	 /detailedSummary:False `
	 /verbosity:quiet `
	/clp:ErrorsOnly;
	if ($LASTEXITCODE -ne 0) {
		throw "Deploying latest code failed. See MSBuild errors above."
	}

	# Wait for Traefik to expose CM route
	Write-Host
	Write-Host "Waiting for CM to become available..." -ForegroundColor Green
	Show-Command "Invoke-RestMethod `"http://localhost:8079/api/http/routers/cm-secure@docker`""
	$startTime = Get-Date
	$status = $null
	do {
		Start-Sleep -Milliseconds 100
		try {
			$status = Invoke-RestMethod "http://localhost:8079/api/http/routers/cm-secure@docker"
		} catch {
			if ($null -eq $_.Exception.Response -or $_.Exception.Response.StatusCode.value__ -ne 404) {
				throw
			}
		}
	} while (($null -eq $status -or $status.status -ne "enabled") -and $startTime.AddSeconds(15) -gt (Get-Date))
	if ($null -eq $status -or $status.status -ne "enabled") {
		if ($null -ne $status) {
			$status
		}
		Write-Error "Timeout waiting for Sitecore CM to become available via Traefik proxy. Check CM container logs."
	}	
}

function Resolve-MSBuildPath {
	$msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
	if ($null -ne $msbuildCommand) {
		return $msbuildCommand.Source
	}

	$knownPaths = @(
		"${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
		"${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
		"${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
		"${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
		"${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
		"${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
		"${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
		"${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
	)

	foreach ($path in $knownPaths) {
		if (Test-Path -Path $path -PathType Leaf) {
			return $path
		}
	}

	$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
	if (Test-Path -Path $vswherePath -PathType Leaf) {
		$resolvedPath = & $vswherePath -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
		if (-not [string]::IsNullOrWhiteSpace($resolvedPath) -and (Test-Path -Path $resolvedPath -PathType Leaf)) {
			return $resolvedPath
		}
	}

	throw "MSBuild was not found. Install Visual Studio Build Tools with the MSBuild and ASP.NET web build tools workloads, or add MSBuild.exe to PATH."
}

function Initialize-DockerBindMounts {
    param(
        [ValidateNotNullOrEmpty()]
        [string]
        $DockerRoot
    )

    $resolvedDockerRoot = (Resolve-Path $DockerRoot).Path
    $paths = @(
        (Join-Path $resolvedDockerRoot "deploy\platform"),
        (Join-Path $resolvedDockerRoot "data\cm"),
        (Join-Path $resolvedDockerRoot "data\sql"),
        (Join-Path $resolvedDockerRoot "data\solr")
    )

    Write-Host
    Write-Host "Ensuring Docker bind mount folders exist..." -ForegroundColor Green
    foreach ($path in $paths) {
        if (-not (Test-Path -Path $path -PathType Container)) {
            Show-Command "New-Item -ItemType Directory -Force -Path `"$path`""
            New-Item -ItemType Directory -Force -Path $path | Out-Null
        }
    }
}


Export-ModuleMember -Function *
