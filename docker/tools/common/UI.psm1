using namespace System.Management.Automation.Host

Set-StrictMode -Version Latest

function Show-Command {  
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $Command
    )
    Write-Host "[COMMAND] $Command" -ForegroundColor Cyan
}

function Show-PrePrompt {
    Write-Host "> " -NoNewline -ForegroundColor Yellow
}

function Confirm {    
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] 
        $Question,
        [switch] 
        $DefaultYes
    )
    $options = [ChoiceDescription[]](
        [ChoiceDescription]::new("&Yes"), 
        [ChoiceDescription]::new("&No")
    )
    $defaultOption = 1;
    if ($DefaultYes) { $defaultOption = 0 }
    Write-Host
    Show-PrePrompt
    $result = $host.ui.PromptForChoice("", $Question, $options, $defaultOption)
    switch ($result) {
        0 { return $true }
        1 { return $false }
    }
}

function Write-PauseDanceAnim {
    # Credit: https://www.reddit.com/r/PowerShell/comments/i1bnfw/a_stupid_little_animation_script/
    param (
        [ValidateNotNullOrEmpty()]
        [int] 
        $PauseInSeconds=10
    )
    $i = 0
    $cursorSave  = (Get-Host).UI.RawUI.cursorsize
    $colors = "Red", "Yellow","Green", "Cyan", "Blue", "Magenta"
    (Get-Host).UI.RawUI.cursorsize = 0
    do {
        "`t`t`t`t(>'-')>", "`t`t`t`t^('-')^", "`t`t`t`t<('-'<)", "`t`t`t`t^('-')^" | % { 
            Write-Host "`r$($_)" -NoNewline -ForegroundColor $colors[$i % 6]
            Start-Sleep -Milliseconds 250
        }
        $i++
    } until ($i -eq $PauseInSeconds)
    (Get-Host).UI.RawUI.cursorsize = $cursorSave
}

Export-ModuleMember -Function *