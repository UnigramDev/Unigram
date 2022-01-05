param (
  [string]$path = $(throw "-path is required"),
  [switch]$compare = $false
)

$path = Resolve-Path $path
$path_strings = "${path}\Strings"

$languages = Get-ChildItem $path_strings -Directory

$values = [System.Collections.ArrayList]@()

if ($compare)
{
    [xml]$androidXml = Get-Content "${path}\Strings\en\Android.resw"
    [xml]$desktopXml = Get-Content "${path}\Strings\en\Desktop.resw"

    $temp = [System.Collections.ArrayList]@()

    if ($compare)
    {
        foreach ($data in $androidXml.SelectNodes("//data"))
        {
            $a = $temp.Add($data.Value)
        }
    }

    foreach ($data in $desktopXml.SelectNodes("//data"))
    {
        if ($compare -And $temp.Contains($data.Value))
        {
            $a = $values.Add($data.name)
        }
    }
}

function Merge([string]$path)
{
    [xml]$androidXml = Get-Content "${path}\Android.resw"
    [xml]$desktopXml = Get-Content "${path}\Desktop.resw"

    $newNode = $androidXml.ImportNode($desktopXml.get_DocumentElement(), $true)

    foreach ($data in $newNode.SelectNodes("//data"))
    {
        if ($compare -And $values.Contains($data.name))
        {
            continue
        }

        $a = $androidXml.DocumentElement.AppendChild($data)
    }

    $androidXml.Save("${path}\Resources.resw")
}

foreach ($language in $languages)
{
    $name = [System.IO.Path]::GetFileName($language)

    if ((Test-Path "${language}\Android.resw") -And (Test-Path "${language}\Desktop.resw"))
    {
        $android = Get-ChildItem "${language}\Android.resw"
        $desktop = Get-ChildItem "${language}\Desktop.resw"

        if (Test-Path "${language}\Resources.resw")
        {
            $resources = Get-ChildItem "${language}\Resources.resw"

            if ($android.LastWrittenTime -gt $resources.LastAccessTime -Or $desktop.LastWriteTime -gt $resources.LastWriteTime)
            {
                Merge -path $language
                Write-Host "Updated ${name}"
            }
        }
        else
        {
            Merge -path $language
            Write-Host "Updated ${name}"
        }
    }
    else
    {
        Copy-Item "${language}\Android.resw" -Destination "${language}\Resources.resw" -Force
        Write-Host "Updated ${name}"
    }
}
