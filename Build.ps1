param (
  [string]$arch = "x64|arm64",
  [string]$mode = "SideloadOnly"
)

.\UpdateManifest.ps1 -path "Telegram.Msix\\" -config "RELEASE" -mode "$mode"
msbuild Telegram.slnx /target:Telegram_Msix /p:Configuration=Release /p:Platform="$arch" /p:UapAppxPackageBuildMode=$mode /p:AppxBundlePlatforms="$arch" /p:AppxBundle=Always /p:AppxPackageSigningEnabled=True