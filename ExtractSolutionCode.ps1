[CmdletBinding()]
param (
    [string]$solutionDir = (Get-Location),
    [string]$outputFile = "SolutionSourceCode.txt"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Should-Process-File {
    param ([string]$fileName)
    if ($fileName -like 'appsettings_*') { return $false }
    return @(".cs", ".xaml") -contains [System.IO.Path]::GetExtension($fileName)
}

function Remove-CSharpComments {
    param ([string[]]$content)
    
    $inComment = $false
    $cleanedContent = foreach ($line in $content) {
        if ($line -match '^\s*///') { continue }
        
        if ($inComment) {
            if ($line -match '\*/') {
                $inComment = $false
                $line = $line -replace '^.*\*/', ''
            } else { continue }
        }
        
        if ($line -match '/\*') {
            $inComment = $true
            $line = $line -replace '/\*.*$', ''
            if ($line.Trim() -eq '') { continue }
        }
        
        $line = $line -replace '//.*$', ''
        if ($line.Trim() -ne '') { $line }
    }
    return $cleanedContent
}

function Get-FileContent {
    param (
        [string]$path,
        [string]$relativePath
    )
    
    try {
        # Lecture avec détection automatique de l'encodage
        $stream = [System.IO.File]::OpenRead($path)
        $reader = New-Object System.IO.StreamReader($stream, $true)
        $content = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()

        if ([string]::IsNullOrWhiteSpace($content)) {
            Write-Warning "Empty file: $path"
            return $null
        }

        $lines = $content -split "`r`n|`r|`n"
        if ($lines.Count -eq 0) {
            Write-Warning "No lines found in: $path"
            return $null
        }

        if ([System.IO.Path]::GetExtension($path) -eq '.cs') {
            $lines = Remove-CSharpComments $lines
        }

        $sb = New-Object System.Text.StringBuilder
        $escapedPath = [Security.SecurityElement]::Escape($relativePath)
        
        $sb.AppendLine("<file path=`"$escapedPath`">").AppendLine() | Out-Null
        foreach ($line in $lines) {
            if (![string]::IsNullOrEmpty($line)) {
                $escapedLine = [Security.SecurityElement]::Escape($line)
                $sb.AppendLine($escapedLine) | Out-Null
            }
        }
        $sb.AppendLine().AppendLine("</file>").AppendLine() | Out-Null
        
        return $sb.ToString()
    }
    catch {
        Write-Warning "Failed to process file: $path"
        Write-Warning $_.Exception.Message
        return $null
    }
}

function Process-Directory {
    param (
        [string]$path,
        [string]$basePath
    )
    
    Get-ChildItem -Path $path -ErrorAction Stop | ForEach-Object {
        if ($_.Name -notmatch '^\.git$|^\.vs$|^bin$|^obj$|^packages$') {
            if ($_.PSIsContainer) {
                Process-Directory $_.FullName $basePath
            }
            elseif (Should-Process-File $_.Name) {
                $relativePath = $_.FullName.Substring($basePath.Length + 1)
                $content = Get-FileContent $_.FullName $relativePath
                if ($content) {
                    Add-Content -Path $outputFile -Value $content -Encoding UTF8
                }
            }
        }
    }
}

try {
    if (!(Test-Path $solutionDir -PathType Container)) {
        throw "Solution directory not found: $solutionDir"
    }
    
    if (Test-Path $outputFile) { Remove-Item $outputFile }
    
    Set-Content -Path $outputFile -Value "<?xml version=`"1.0`" encoding=`"UTF-8`"?>" -Encoding UTF8
    Add-Content -Path $outputFile -Value "<solution>" -Encoding UTF8
    Add-Content -Path $outputFile -Value "" -Encoding UTF8
    
    Process-Directory $solutionDir $solutionDir
    
    Add-Content -Path $outputFile -Value "</solution>" -Encoding UTF8
    Write-Host "Solution source code saved to $outputFile"
}
catch {
    Write-Error "Failed to extract solution code: $_"
    exit 1
}