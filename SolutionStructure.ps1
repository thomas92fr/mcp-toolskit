# SolutionStructure.ps1
# Ce script extrait la structure de la solution dans un fichier texte
$solutionDir = Get-Location
$outputFile = "SolutionStructure.txt"

function Get-DirectoryStructure {
    param (
        [string]$path,
        [string]$indent = ""
    )
    
    $items = Get-ChildItem -Path $path
    
    foreach ($item in $items) {
        if ($item.Name -notmatch '^\.git$|^\.vs$|^bin$|^obj$|packages$') {
            Add-Content -Path $outputFile -Value ($indent + $item.Name)
            
            if ($item.PSIsContainer) {
                Get-DirectoryStructure -path $item.FullName -indent ($indent + "    ")
            }
        }
    }
}

# Supprimer le fichier s'il existe déjà
if (Test-Path $outputFile) {
    Remove-Item $outputFile
}

Get-DirectoryStructure -path $solutionDir

Write-Host "Structure de la solution sauvegardée dans $outputFile"