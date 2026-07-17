$roots = @(
    (Join-Path $PSScriptRoot "deploy"),
    (Join-Path $PSScriptRoot "data")
)

foreach ($root in $roots) {
    if (-not (Test-Path -Path $root -PathType Container)) {
        continue
    }

    Get-ChildItem -Path $root -Directory | ForEach-Object {
        Get-ChildItem -Path $_.FullName -Exclude ".gitkeep" -Recurse |
            Remove-Item -Force -Recurse -Verbose
    }
}
