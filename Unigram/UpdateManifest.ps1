param (
  [string]$path = $(throw "-path is required"),
  [string]$config = "DEBUG"
)

Write-Output $config
Write-Output $path

$path = Resolve-Path $path
$path_manifest = "${path}\Package.appxmanifest"

$config = $config.ToUpper()

$pinfo = New-Object System.Diagnostics.ProcessStartInfo
$pinfo.FileName = "git"
$pinfo.Arguments = "rev-list --count HEAD"
$pinfo.WorkingDirectory = $path
$pinfo.RedirectStandardOutput = $true
$pinfo.UseShellExecute = $false
$pinfo.CreateNoWindow = $true
$p = New-Object System.Diagnostics.Process
$p.StartInfo = $pinfo
$p.Start() | Out-Null
$p.WaitForExit()
$stdout = $p.StandardOutput.ReadLine()
Write-Host "stdout: '$stdout'"
Write-Host "exit code: " + $p.ExitCode

[xml]$document = Get-Content $path_manifest

$h = @{}
$h["DEBUG"] = "38833FF26BA1D.UnigramExperimental"
$h["RELEASE"] = "38833FF26BA1D.UnigramPreview"

$identity = $document.GetElementsByTagName("Identity")[0]
$identity.Attributes["Name"].Value = $h[$config]

$version = $identity.Attributes["Version"].Value;
$regex = [regex]'(?:(\d+)\.)(?:(\d+)\.)(?:(\d+)\.\d+)'

$identity.Attributes["Version"].Value = $regex.Replace($version, '$1.$2.' + $stdout + '.0')

$h = @{}
$h["DEBUG"] = "Unigram Experimental"
$h["RELEASE"] = "Unigram - A Telegram universal experience"

$properties = $document.GetElementsByTagName("Properties")[0]
$displayName = $properties.GetElementsByTagName("DisplayName")[0]
$displayName.InnerText = $h[$config]

$h = @{}
$h["DEBUG"] = "Telegram"
$h["RELEASE"] = "Unigram"

$visualElements = $document.GetElementsByTagName("uap:VisualElements")[0]
$visualElements.Attributes["DisplayName"].Value = $h[$config]

$document.Save($path_manifest)