param (
  [string]$path = $(throw "-path is required"),
  [switch]$compare = $false
)

$path = Resolve-Path $path
$path_strings = "${path}Strings\"

$languages = Get-ChildItem $path_strings -Directory

$values = [System.Collections.ArrayList]@()

if ($compare)
{
    [xml]$androidXml = Get-Content "${path_strings}en\Android.resw"
    [xml]$desktopXml = Get-Content "${path_strings}en\Desktop.resw"

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
    [xml]$androidXml = Get-Content "${path}\Android.resw" -Encoding UTF8
    [xml]$desktopXml = Get-Content "${path}\Desktop.resw" -Encoding UTF8

    $newNode = $androidXml.ImportNode($desktopXml.get_DocumentElement(), $true)

    foreach ($data in $newNode.SelectNodes("//data"))
    {
        if ($compare -And $values.Contains($data.name))
        {
            continue
        }

        $a = $androidXml.DocumentElement.AppendChild($data)
    }

    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($true)
    $sw = New-Object System.IO.StreamWriter("${path}\Resources.resw", $false, $utf8WithoutBom)

    $androidXml.Save($sw)
    $sw.Close()
}

foreach ($language in $languages)
{
    $path_language = "${path_strings}${language}"
    $name = [System.IO.Path]::GetFileName($language)
        
    if ((Test-Path "${path_language}\Android.resw") -And (Test-Path "${path_language}\Desktop.resw"))
    {
        $android = Get-ChildItem "${path_language}\Android.resw"
        $desktop = Get-ChildItem "${path_language}\Desktop.resw"

        if (Test-Path "${path_language}\Resources.resw")
        {
            $resources = Get-ChildItem "${path_language}\Resources.resw"

            if ($android.LastWriteTime -gt $resources.LastWriteTime -Or $desktop.LastWriteTime -gt $resources.LastWriteTime)
            {
                Merge -path $path_language
                Write-Host "Updated ${name}"
            }
        }
        else
        {
            Merge -path $path_language
            Write-Host "Updated ${name}"
        }
    }
    else
    {
        Copy-Item "${path_language}\Android.resw" -Destination "${path_language}\Resources.resw" -Force
        Write-Host "Updated ${name}"
    }
}
