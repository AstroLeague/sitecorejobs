Import-Module -Name (Join-Path $PSScriptRoot "..\common\UI") -Force -DisableNameChecking

Set-StrictMode -Version Latest

function Install-SitecoreDockerTools {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $dockerToolsVersion # "10.3.0"
	)
	
    Import-Module PowerShellGet
    $SitecoreGallery = Get-PSRepository | Where-Object { $_.SourceLocation -eq "https://sitecore.myget.org/F/sc-powershell/api/v2" }
    if (-not $SitecoreGallery) {
        Write-Host
        Write-Host "Adding Sitecore PowerShell Gallery..." -ForegroundColor Green 
		Show-Command "Register-PSRepository -Name SitecoreGallery -SourceLocation https://sitecore.myget.org/F/sc-powershell/api/v2 -InstallationPolicy Trusted"
        Register-PSRepository -Name SitecoreGallery -SourceLocation https://sitecore.myget.org/F/sc-powershell/api/v2 -InstallationPolicy Trusted
        $SitecoreGallery = Get-PSRepository -Name SitecoreGallery
    }
    
    Remove-Module SitecoreDockerTools -ErrorAction SilentlyContinue
    if (-not (Get-InstalledModule -Name SitecoreDockerTools -RequiredVersion $dockerToolsVersion -ErrorAction SilentlyContinue)) {
        Write-Host
        Write-Host "Installing SitecoreDockerTools $($dockerToolsVersion)..." -ForegroundColor Green
		Show-Command "Install-Module SitecoreDockerTools -RequiredVersion $dockerToolsVersion -Scope CurrentUser -Repository $SitecoreGallery.Name"
        Install-Module SitecoreDockerTools -RequiredVersion $dockerToolsVersion -Scope CurrentUser -Repository $SitecoreGallery.Name
    }
    
    Write-Host
    Write-Host "Importing SitecoreDockerTools $($dockerToolsVersion)..." -ForegroundColor Green
	Show-Command "Import-Module SitecoreDockerTools -RequiredVersion $dockerToolsVersion"
    Import-Module SitecoreDockerTools -RequiredVersion $dockerToolsVersion
}

function Get-EnvValueByKey {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $Key,        
        [ValidateNotNullOrEmpty()]
        [string] 
        $FilePath = ".env",
        [ValidateNotNullOrEmpty()]
        [string] 
        $DockerRoot = ".\docker"
    )
    if (!(Test-Path $FilePath)) {
        $FilePath = Join-Path $DockerRoot $FilePath
    }
    if (!(Test-Path $FilePath)) {
        return ""
    }
    select-string -Path $FilePath -Pattern "^$Key=(.+)$" | % { $_.Matches.Groups[1].Value }
}

function Initialize-HostNames {
    param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $HostDomain,
		[string[]]
		$AdditionalSubDomains= @()
    )
    Write-Host
    Write-Host "Adding hosts file entries..." -ForegroundColor Green
    
	$StandardNames = @('cm', 'id');
	foreach ($name in $StandardNames)
    {
		Write-Host " > $($name).$($HostDomain)" -ForegroundColor Green
		
		Show-Command "Remove-HostsEntry `"$name.$HostDomain`""
        Remove-HostsEntry "$name.$HostDomain"
		Show-Command "Add-HostsEntry `"$name.$HostDomain`""
		Add-HostsEntry "$name.$HostDomain"
    }
	
	foreach ($name in $AdditionalSubDomains)
    {
		Write-Host " > $($name).$($HostDomain)" -ForegroundColor Green
		
		Show-Command "Remove-HostsEntry `"$name.$HostDomain`""
        Remove-HostsEntry "$name.$HostDomain"
		Show-Command "Add-HostsEntry `"$name.$HostDomain`""
		Add-HostsEntry "$name.$HostDomain"
    }
}

function Initialize-HostNames-ForApp {
    param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $AppFolder,
		[string[]]
		$AdditionalSubDomains = @()
    )
	
	$hostDomain = Get-EnvValueByKey -Key "HOST_DOMAIN" -DockerRoot $AppFolder
	if ($hostDomain -eq "") {
		throw "Required variable 'HOST_DOMAIN' not set in .env file."
	}
	
	Initialize-HostNames $hostDomain $AdditionalSubDomains
}

function Init-SitecoreCli {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $version # "5.1.25"
	)
	
    Write-Host
    Write-Host "Ensuring Sitecore CLI $($version) is installed..." -ForegroundColor Green
	
	Show-Command "dotnet new tool-manifest --force"
	dotnet new tool-manifest --force

	# dotnet tool restore
	
	Show-Command "dotnet tool install Sitecore.CLI --version $version"
	dotnet tool install Sitecore.CLI --version $version

	Show-Command "dotnet sitecore plugin add --version $version -n Sitecore.DevEx.Extensibility.Indexing"
	dotnet sitecore plugin add --version $version -n Sitecore.DevEx.Extensibility.Indexing

	# dotnet sitecore plugin list
}

function Init-SitecoreIndexes {
    param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $dockerFolder,
		[string[]]
		$indexNames = @()
	)	
   
	$cmHost = Get-EnvValueByKey -Key "CM_HOST" -DockerRoot $dockerFolder
	$idHost = Get-EnvValueByKey -Key "ID_HOST" -DockerRoot $dockerFolder
    Write-Host
	Write-Host "Initializing Sitecore CLI. You will be prompted from $cmHost to grant write access..." -ForegroundColor Green
	
	Show-Command "dotnet sitecore login --cm `"https://$cmHost`" --auth `"https://$idHost`" --allow-write true"
	dotnet sitecore login --cm "https://$cmHost" --auth "https://$idHost" --allow-write true
	   
    Write-Host
    Write-Host "Initializing Sitecore Indexes..." -ForegroundColor Green
	Write-Host "(1 of 2) Populating Solr schema..." -ForegroundColor Green
	# NOTE: IF you have coveo or other non-sitecore indexes you must explicitly list sitecore ones here to avoid error
	Show-Command "dotnet sitecore index schema-populate --indexes $indexNames"
	dotnet sitecore index schema-populate --indexes $indexNames

	Write-Host "(2 of 2) Rebuilding indexes..." -ForegroundColor Green
	Show-Command "dotnet sitecore index rebuild --indexes $indexNames"
	dotnet sitecore index rebuild --indexes $indexNames
}

function Invoke-DockerBuild {
    param(
        [ValidateNotNullOrEmpty()]
        [string] 
        $DockerRoot = ".\docker"
    )
	
	Write-Host
	Write-Host "Building Images..." -ForegroundColor Green
		
	Write-Host
	Write-Host "Entering $DockerRoot" -ForegroundColor Green
	Show-Command "cd $DockerRoot"
	Push-Location $DockerRoot
	
	try {
	
	    Write-Host
	    Show-Command "docker compose build -m 2GB --progress plain" # --no-cache
	    docker compose build -m 2GB --progress plain # --no-cache
        
	    Write-Host
	    Write-Host "Docker images ready!" -ForegroundColor Green

    } finally {
		Pop-Location
    }
}

Export-ModuleMember -Function *
