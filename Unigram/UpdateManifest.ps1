param (
  [string]$path = $(throw "-path is required"),
  [string]$config = "DEBUG",
  [string]$store = "true",
  [string]$sign = "true"
)

#exit

Write-Output "Config: $config"
Write-Output "Path: $path"
Write-Output "Store: $store"
Write-Output "Sign: $sign"

$path = Resolve-Path $path
$path_manifest = "${path}\Package.appxmanifest"

$config = $config.ToUpper()

if ($config -eq "RELEASE") {
    if ($sign -eq "false" -and $store -eq "false") {
        $config = "DIRECT"
    }
    elseif ("store" -eq "false") {
        $config = "DEBUG"
    }
}


try {
    $out = Invoke-Command -ScriptBlock {git -C $path rev-list --count HEAD}
} catch {
    exit
}

Write-Host "Git rev-list: $out"

$rtn = 0
if ([double]::TryParse($out, [ref]$rtn) -ne $true) {
    exit
}

[xml]$document = Get-Content $path_manifest

$h = @{}
$h["DEBUG"] = @{
    Name = "38833FF26BA1D.UnigramExperimental";
    Publisher = "CN=D89C87B4-2758-402A-8F40-3571D00882AB";
    DisplayName = "Unigram Experimental";
    PublisherDisplayName = "Unigram, Inc.";
    AppName = "Telegram"
}
$h["RELEASE"] = @{
    Name = "38833FF26BA1D.UnigramPreview";
    Publisher = "CN=D89C87B4-2758-402A-8F40-3571D00882AB";
    DisplayName = "Unigram - Telegram for Windows 10";
    PublisherDisplayName = "Unigram, Inc.";
    AppName = "Unigram"
}
$h["DIRECT"] = @{
    Name = "TelegramFZ-LLC.Unigram";
    Publisher = 'CN=Telegram FZ-LLC, O=Telegram FZ-LLC, POBox=501, L=Dubai, C=AE, OID.2.5.4.15=Private Organization, OID.1.3.6.1.4.1.311.60.2.1.3=AE, SERIALNUMBER=94349';
    DisplayName = "Unigram - Telegram for Windows 10";
    PublisherDisplayName = "Telegram FZ-LLC";
    AppName = "Unigram"
}

$identity = $document.GetElementsByTagName("Identity")[0]
$identity.Attributes["Name"].Value = $h[$config].Name
$identity.Attributes["Publisher"].Value = $h[$config].Publisher

$version = $identity.Attributes["Version"].Value
$regex = [regex]'(?:(\d+)\.)(?:(\d+)\.)(?:(\d*?)\.\d+)'

$identity.Attributes["Version"].Value = $regex.Replace($version, '$1.$2.{0}.0' -f $out)

$properties = $document.GetElementsByTagName("Properties")[0]
$displayName = $properties.GetElementsByTagName("DisplayName")[0]
$displayName.InnerText = $h[$config].DisplayName

$publisherDisplayName = $properties.GetElementsByTagName("PublisherDisplayName")[0]
$publisherDisplayName.InnerText = $h[$config].PublisherDisplayName

$visualElements = $document.GetElementsByTagName("uap:VisualElements")[0]
$visualElements.Attributes["DisplayName"].Value = $h[$config].AppName

$document.Save("$path_manifest.tmp")

if(Compare-Object -ReferenceObject $(Get-Content $path_manifest) -DifferenceObject $(Get-Content "$path_manifest.tmp")) {
    $document.Save($path_manifest)
    Write-Output "Package.appxmanifest updated"
}

Remove-Item "$path_manifest.tmp"

$storeAssociation = Get-Content "${path}\Package.StoreAssociation.xml"

$publisher = $h[$config].Publisher
$publisherDisplayName = $h[$config].PublisherDisplayName

$storeAssociation = $storeAssociation -replace "<Publisher>(.*?)</Publisher>", "<Publisher>$publisher</Publisher>"
$storeAssociation = $storeAssociation -replace "<PublisherDisplayName>(.*?)</PublisherDisplayName>", "<PublisherDisplayName>$publisherDisplayName</PublisherDisplayName>"

if(Compare-Object -ReferenceObject $(Get-Content "${path}\Package.StoreAssociation.xml") -DifferenceObject $storeAssociation) {
    Set-Content -Path "${path}\Package.StoreAssociation.xml" -Value $storeAssociation
    Write-Output "Package.StoreAssociation.xml updated"
}
