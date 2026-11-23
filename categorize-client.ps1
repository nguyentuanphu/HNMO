param(
    [string]$InputCsv,
    [string]$OutputCsv
)

$csv = Import-Csv $InputCsv
foreach ($row in $csv) {
    $ext = $row.kind.ToLower()
    $path = $row.path.ToLower()
    
    # Determine kind for client files
    if ($ext -eq '.ozj' -or $ext -eq '.ozt' -or $ext -eq '.ozb' -or $ext -eq '.ozd' -or $ext -eq '.ozg' -or $ext -eq '.ozp') {
        $row.kind = 'binary data'
        $row.has_rules = 'no'
    } elseif ($ext -eq '.bmd' -or $ext -eq '.obj' -or $ext -eq '.map' -or $ext -eq '.att' -or $ext -eq '.hit') {
        $row.kind = 'binary data'
        $row.has_rules = 'no'
    } elseif ($ext -eq '.wav' -or $ext -eq '.mp3' -or $ext -eq '.ogg') {
        $row.kind = 'art/sound'
        $row.has_rules = 'no'
    } elseif ($ext -eq '.tga' -or $ext -eq '.jpg' -or $ext -eq '.png' -or $ext -eq '.bmp' -or $ext -eq '.psd') {
        $row.kind = 'art/sound'
        $row.has_rules = 'no'
    } elseif ($ext -eq '.xml' -or $ext -eq '.ini' -or $ext -eq '.dat' -or $ext -eq '.txt' -or $ext -eq '.cfg') {
        if ($path -like '*item*' -or $path -like '*skill*' -or $path -like '*monster*' -or $path -like '*gate*' -or $path -like '*shop*' -or $path -like '*mix*' -or $path -like '*socket*' -or $path -like '*exp*' -or $path -like '*event*' -or $path -like '*quest*') {
            $row.kind = 'data table'
            $row.has_rules = 'yes'
        } else {
            $row.kind = 'data table'
            $row.has_rules = 'maybe'
        }
    } elseif ($ext -eq '.exe' -or $ext -eq '.dll') {
        $row.kind = 'program'
        $row.has_rules = 'no'
    } elseif ($ext -eq '.lua') {
        $row.kind = 'script'
        $row.has_rules = 'maybe'
    } elseif ($ext -eq '.db') {
        $row.kind = 'data table'
        $row.has_rules = 'yes'
    } else {
        $row.kind = 'other'
        $row.has_rules = 'no'
    }
}
$csv | Export-Csv -Path $OutputCsv -NoTypeInformation