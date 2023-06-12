param (
  [string]$arch = "x86|x64|arm64",
  [string]$mode = "SideloadOnly"
)

if ($mode -eq "SideloadOnly") {
  $certificate = "PackageCertificateThumbprint=60FFAEE648D4D34A1089405643B511F50BEF8A49"
} else {
  $certificate = "PackageCertificateKeyFile=Telegram.Msix_TemporaryKey.pfx"
}

.\UpdateManifest.ps1 -path "Telegram.Msix\\" -config "RELEASE" -mode "$mode"
msbuild Telegram.sln /target:Telegram_Msix /p:Configuration=Release /p:Platform="$arch" /p:UapAppxPackageBuildMode=$mode /p:AppxBundlePlatforms="$arch" /p:AppxBundle=Always /p:AppxPackageSigningEnabled=True /p:$certificate