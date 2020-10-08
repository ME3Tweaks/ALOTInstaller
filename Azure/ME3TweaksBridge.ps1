param (
    [string]$Username = $(throw "-Username is required"),
    [string]$Password = $( Read-Host -asSecureString "Input password" ),
    [string]$InputFile = $(throw "-InputFile is required"),
    [string]$Endpoint = $(throw "-Endpoint is required"),
    [string]$Version = $(throw "-Version string is required"),
    [string]$AppName = $(throw "-AppName is required")

)

# Authentication
$Password
[securestring]$secStringPassword = ConvertTo-SecureString $Password -AsPlainText -Force
[pscredential]$cred = New-Object System.Management.Automation.PSCredential ($Username, $secStringPassword)
$secStringPassword = $null
$Password = $null

# Fields
$headers = @{
    INCOMINGFILENAME = (Get-Item $InputFile).Name
    INCOMINGFILESIZE = (Get-Item $InputFile).Length
    INCOMINGMD5 = (Get-FileHash $InputFile -Algorithm MD5).Hash.ToLower()
    INCOMINGVERSION = $Version
    INCOMINGAPPNAME = $AppName
}


$Response = $null # For retesting
$Response = Invoke-WebRequest -Uri $Endpoint -Credential $cred -Headers $headers -Method Post -InFile $InputFile
exit $(if ($Response.StatusCode -eq 200) {0} Else {$Response.StatusCode}) 