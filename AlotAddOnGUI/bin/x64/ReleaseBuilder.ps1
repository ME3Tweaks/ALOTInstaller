get-childitem "$($PSScriptRoot)\Release\" -include *.7z -recurse | foreach ($_) {remove-item $_.fullname}
if (Test-Path "$($PSScriptRoot)\Release\logs") {
    Write-Host Removing log files...
    Remove-Item "$($PSScriptRoot)\Release\logs" -Force -Recurse
}

if (Test-Path "$($PSScriptRoot)\Release\lib\manifest.xml") {
    Write-Host Removing lib manifest file...
    Remove-Item "$($PSScriptRoot)\Release\lib\manifest.xml" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\manifest.xml") {
    if (Test-Path "$($PSScriptRoot)\Release\manifest-bundled.xml") {
        Write-Host Removing existing bundled manifest...
        Remove-Item "$($PSScriptRoot)\Release\manifest-bundled.xml" -Force
    }
    Write-Host Making bundled xml file...
    Move-Item "$($PSScriptRoot)\Release\manifest.xml"  -Destination "$($PSScriptRoot)\Release\manifest-bundled.xml" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\lib\AlotAddonBuilder.pdb") {
    Write-Host Moving PDB...
    Move-Item "$($PSScriptRoot)\Release\lib\AlotAddonBuilder.pdb"  -Destination "$($PSScriptRoot)\Release\AlotAddonBuilder.pdb" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\lib\MahApps.Metro.pdb") {
    Write-Host Removing mahapps pdb...
    Remove-Item "$($PSScriptRoot)\Release\lib\MahApps.Metro.pdb" -Force
}



$fileversion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("Release\AlotAddonBuilder.exe").FileVersion
$outputfile = "$($PSScriptRoot)\ALOTAddonBuilder_$($fileversion).7z"
$exe = "$($PSScriptRoot)\Release\bin\7z.exe"
$arguments = "a", "`"$($outputfile)`"", "`"$($PSScriptRoot)\Release\*`"", "-mmt6"
Write-Host "Running: $($exe) $($arguments)"
Start-Process $exe -ArgumentList $arguments -Wait -NoNewWindow