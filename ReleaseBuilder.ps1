get-childitem "$($PSScriptRoot)\Release\" -include *.7z -recurse | foreach ($_) {remove-item $_.fullname}

$fileversion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("Release\AlotAddonBuilder.exe").FileVersion
$outputfile = "$($PSScriptRoot)\Release\ALOTAddonBuilder_$($fileversion).7z"
$exe = "$($PSScriptRoot)\Release\\bin\7z.exe"
$arguments = "a", "`"$($outputfile)`"", "`"$($PSScriptRoot)\Release\*`"", "-mmt6"
Write-Host "Running: $($exe) $($arguments)"
Start-Process $exe -ArgumentList $arguments -Wait -NoNewWindow