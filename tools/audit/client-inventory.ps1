#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Inventory client files for HNMO client audit
.DESCRIPTION
    Categorizes files in four folders by kind and has_rules
#>

param(
    [string]$S6ClientDir = "D:\WorkSpace\MuOnline\IGCN\IGCN x6\IGC.Evi.Network x6.3 6.0.0.1 Full Client\IGC.Evi.Network x6.3 Full Client",
    [string]$S9ClientDir = "D:\WorkSpace\MuOnline\IGCN\IGCN x9\IGC.Evi.Network x9 Full Client\Season 9",
    [string]$WebSrcDir = "D:\WorkSpace\HNMO-src\client\web\src",
    [string]$WebDataDir = "D:\WorkSpace\HNMO-src\client\web\public\data",
    [string]$OutDir = "D:\WorkSpace\HNMO-src\docs\audit\client-inventory"
)

# Create output directory
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

function Get-FileKind {
    param([string]$Path)
    $ext = [System.IO.Path]::GetExtension($Path).ToLower()
    $name = [System.IO.Path]::GetFileName($Path).ToLower()
    
    # Art/Sound
    if ($ext -in @('.bmd', '.ozj', '.ozb', '.jpg', '.jpeg', '.png', '.bmp', '.tga', '.dds', '.ogg', '.wav', '.mp3', '.mid', '.midi')) {
        return "art_sound"
    }
    # Data tables
    if ($ext -in @('.txt', '.json', '.bmd', '.dat', '.bmd')) {
        if ($name -match '(item|skill|monster|map|gate|npc|shop|mix|socket|ancient|event|exp|level|quest|spawn|warp|move)') {
            return "data_table"
        }
        return "data_table"
    }
    # UI/Interface
    if ($ext -in @('.rml', '.xml', '.lua', '.rml')) {
        return "ui_interface"
    }
    # Scripts/Config
    if ($ext -in @('.ini', '.cfg', '.config', '.js', '.ts', '.mjs', '.cjs', '.lua', '.py', '.sh', '.bat', '.cmd', '.ps1')) {
        return "script_config"
    }
    # Binaries
    if ($ext -in @('.exe', '.dll', '.so', '.lib', '.a', '.pdb', '.ilk')) {
        return "binary"
    }
    # Source code
    if ($ext -in @('.ts', '.tsx', '.js', '.jsx', '.cs', '.cpp', '.h', '.hpp', '.c', '.cc', '.cxx')) {
        return "source_code"
    }
    return "other"
}

function Get-HasRules {
    param([string]$Path, [string]$Kind)
    $ext = [System.IO.Path]::GetExtension($Path).ToLower()
    $name = [System.IO.Path]::GetFileName($Path).ToLower()
    
    # Files that likely contain game rules/logic
    if ($Kind -eq "data_table") { return "yes" }
    if ($Kind -eq "script_config") { 
        if ($name -match '(config|setting|script|ai|behavior|logic|rule)') { return "yes" }
        return "maybe" 
    }
    if ($Kind -eq "ui_interface") { return "maybe" }
    if ($Kind -eq "source_code") { return "yes" }
    return "no"
}

function Inventory-Folder {
    param([string]$Dir, [string]$Label, [string]$OutFile)
    
    Write-Host "Inventorying ${Label}: $Dir"
    $files = Get-ChildItem -Path $Dir -Recurse -File -ErrorAction SilentlyContinue
    $results = @()
    
    foreach ($file in $files) {
        $relPath = $file.FullName.Substring($Dir.Length + 1)
        $kind = Get-FileKind $file.FullName
        $hasRules = Get-HasRules $file.FullName $kind
        $size = $file.Length
        $modified = $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        
        $results += [PSCustomObject]@{
            Label = $Label
            RelativePath = $relPath
            FullPath = $file.FullName
            Extension = [System.IO.Path]::GetExtension($file.Name)
            Kind = $kind
            HasRules = $hasRules
            SizeBytes = $size
            Modified = $modified
        }
    }
    
    $results | Export-Csv -Path $OutFile -NoTypeInformation -Encoding UTF8
    Write-Host "  -> $($results.Count) files written to $OutFile"
    
    # Summary
    $summary = $results | Group-Object Kind, HasRules | Select-Object @{Name='Kind';Expression={$_.Name.Split(',')[0]}}, @{Name='HasRules';Expression={$_.Name.Split(',')[1]}}, Count
    Write-Host "  Summary:"
    $summary | Format-Table -AutoSize | Out-Host
    
    return $results
}

# Run inventories
$s6Results = Inventory-Folder $S6ClientDir "S6_Client" "$OutDir\s6-client.csv"
$s9Results = Inventory-Folder $S9ClientDir "S9_Client" "$OutDir\s9-client.csv"
$webSrcResults = Inventory-Folder $WebSrcDir "Web_Source" "$OutDir\web-source.csv"
$webDataResults = Inventory-Folder $WebDataDir "Web_Data" "$OutDir\web-data.csv"

# Combined summary
$allResults = $s6Results + $s9Results + $webSrcResults + $webDataResults
$allResults | Export-Csv -Path "$OutDir\all-clients.csv" -NoTypeInformation -Encoding UTF8

Write-Host "`n=== COMBINED SUMMARY ==="
$allResults | Group-Object Label, Kind, HasRules | Select-Object @{Name='Label';Expression={$_.Name.Split(',')[0]}}, @{Name='Kind';Expression={$_.Name.Split(',')[1]}}, @{Name='HasRules';Expression={$_.Name.Split(',')[2]}}, Count | Sort-Object Label, Kind, HasRules | Format-Table -AutoSize

Write-Host "`nDone! Output in $OutDir"