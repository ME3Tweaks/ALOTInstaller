get-childitem "$($PSScriptRoot)\Release\" -include *.7z -recurse | foreach ($_) {remove-item $_.fullname}
if (Test-Path "$($PSScriptRoot)\Release\logs") {
    Write-Host Removing log files...
    Remove-Item "$($PSScriptRoot)\Release\logs" -Force -Recurse
}

if (Test-Path "$($PSScriptRoot)\Release\Data\lib\ALOTInstaller.pdb") {
    Write-Host Moving PDB...
    Move-Item "$($PSScriptRoot)\Release\Data\lib\ALOTInstaller.pdb"  -Destination "$($PSScriptRoot)\Release\ALOTInstaller.pdb" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\ALOTAddonBuilder.exe") {
    Write-Host Removing current ALOTAddonBuilder...
    Remove-Item "$($PSScriptRoot)\Release\ALOTAddonBuilder.exe" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\Data\lib\MahApps.Metro.IconPacks.Modern.pdb") {
    Write-Host Removing MahApps Icon Pack PDB...
    Remove-Item "$($PSScriptRoot)\Release\Data\lib\MahApps.Metro.IconPacks.Modern.pdb" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\Data\lib\MahApps.Metro.pdb") {
    Write-Host Removing MahApps Metro PDB...
    Remove-Item "$($PSScriptRoot)\Release\Data\lib\MahApps.Metro.pdb" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\Data\music") {
    Write-Host Removing music directory...
    Remove-Item "$($PSScriptRoot)\Release\Data\music" -Recurse -Force
}

if (Test-Path "$($PSScriptRoot)\Release\Data\MEM_Packages") {
    Write-Host Removing output directory...
    Remove-Item "$($PSScriptRoot)\Release\Data\MEM_Packages" -Recurse -Force
}


if (Test-Path "$($PSScriptRoot)\Release\Data\Extracted_Mods") {
    Write-Host Removing extraction directory...
    Remove-Item "$($PSScriptRoot)\Release\Data\Extracted_Mods" -Recurse -Force
}

if (Test-Path "$($PSScriptRoot)\Release\Data\manifest-bundled.xml") {
    Write-Host Removing current manifest bundle...
    Remove-Item "$($PSScriptRoot)\Release\Data\manifest-bundled.xml" -Force
}

if (Test-Path "$($PSScriptRoot)\Release\Data\manifest.xml") {
    Write-Host Bundling current manifest...
    Move-Item "$($PSScriptRoot)\Release\Data\manifest.xml" -Destination "$($PSScriptRoot)\Release\Data\manifest-bundled.xml"
}

if (Test-Path "$($PSScriptRoot)\..\..\update-backcompat\ALOTAddonBuilder.exe") {
    Write-Host Adding ALOTAddonBuilder.exe for update backwards compatibility...
    Copy-Item "$($PSScriptRoot)\..\..\update-backcompat\ALOTAddonBuilder.exe" "$($PSScriptRoot)\Release\ALOTAddonBuilder.exe" -Force
}

$Cert = Get-ChildItem -Path "Cert:\CurrentUser\My" -CodeSigningCert
Set-AuthenticodeSignature -FilePath "Release\ALOTInstaller.exe" -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm "SHA256" -Certificate $Cert


$fileversion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("Release\ALOTInstaller.exe").FileVersion
$outputfile = "$($PSScriptRoot)\ALOTInstaller_$($fileversion).7z"
$exe = "$($PSScriptRoot)\Release\Data\bin\7z.exe"
$arguments = "a", "`"$($outputfile)`"", "`"$($PSScriptRoot)\Release\*`"", "-mmt6"
Write-Host "Running: $($exe) $($arguments)"
Start-Process $exe -ArgumentList $arguments -Wait -NoNewWindow