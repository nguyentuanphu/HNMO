param(
    [string]$InputCsv,
    [string]$OutputCsv
)

$csv = Import-Csv $InputCsv
foreach ($row in $csv) {
    $ext = $row.kind.ToLower()
    $path = $row.path.ToLower()
    
    # Determine kind
    if ($ext -eq '.xml' -or $ext -eq '.ini' -or $ext -eq '.dat' -or $ext -eq '.txt' -or $ext -eq '.cfg') {
        if ($path -like '*item*' -or $path -like '*skill*' -or $path -like '*monster*' -or $path -like '*spawn*' -or $path -like '*gate*' -or $path -like '*shop*' -or $path -like '*mix*' -or $path -like '*socket*' -or $path -like '*harmony*' -or $path -like '*excellent*' -or $path -like '*set*' -or $path -like '*price*' -or $path -like '*exp*' -or $path -like '*event*' -or $path -like '*bloodcastle*' -or $path -like '*devilsquare*' -or $path -like '*chaoscastle*' -or $path -like '*kanturu*' -or $path -like '*raklion*' -or $path -like '*ringattack*' -or $path -like '*xmas*') {
            $row.kind = 'data table'
            $row.has_rules = 'yes'
        } elseif ($path -like '*config*' -or $path -like '*setting*' -or $path -like '*common*') {
            $row.kind = 'rule config'
            $row.has_rules = 'yes'
        } else {
            $row.kind = 'data table'
            $row.has_rules = 'maybe'
        }
    } elseif ($ext -eq '.lua') {
        $row.kind = 'script'
        $row.has_rules = 'maybe'
    } elseif ($ext -eq '.exe' -or $ext -eq '.dll') {
        $row.kind = 'program'
        $row.has_rules = 'no'
    } elseif ($ext -eq '.sql') {
        $row.kind = 'data table'
        $row.has_rules = 'yes'
    } elseif ($ext -eq '.att' -or $ext -eq '.hit' -or $ext -eq '.bmd') {
        $row.kind = 'binary data'
        $row.has_rules = 'no'
    } else {
        $row.kind = 'other'
        $row.has_rules = 'no'
    }
}
$csv | Export-Csv -Path $OutputCsv -NoTypeInformation