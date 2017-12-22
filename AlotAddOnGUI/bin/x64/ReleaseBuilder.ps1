get-childitem "$($PSScriptRoot)\Release\" -include *.7z -recurse | foreach ($_) {remove-item $_.fullname}
if (Test-Path "$($PSScriptRoot)\Release\logs") {
    Write-Host Removing log files...
    Remove-Item "$($PSScriptRoot)\Release\logs" -Force -Recurse
}

if (Test-Path "$($PSScriptRoot)\Release\lib\ALOTInstaller.pdb") {
    Write-Host Moving PDB...
    Move-Item "$($PSScriptRoot)\Release\lib\ALOTInstaller.pdb"  -Destination "$($PSScriptRoot)\Release\ALOTInstaller.pdb" -Force
}

$fileversion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("Release\ALOTInstaller.exe").FileVersion
$outputfile = "$($PSScriptRoot)\ALOTInstaller_$($fileversion).7z"
$exe = "$($PSScriptRoot)\Release\bin\7z.exe"
$arguments = "a", "`"$($outputfile)`"", "`"$($PSScriptRoot)\Release\*`"", "-mmt6"
Write-Host "Running: $($exe) $($arguments)"
Start-Process $exe -ArgumentList $arguments -Wait -NoNewWindow