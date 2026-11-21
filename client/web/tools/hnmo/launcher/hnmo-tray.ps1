# HNMO without a console window (owner's choice, 2026-11-21): starts HNMO hidden and puts its icon next to the
# Windows clock, with "Open the game", "Open the admin panel", "Show the log" and "Stop HNMO". If HNMO stops by
# itself, a message box says so and where the log is (save\logs\launcher.log). Uses only what Windows has.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$root = Split-Path -Parent $PSScriptRoot
$vi = (Get-Culture).TwoLetterISOLanguageName -eq 'vi'
function T([string]$en, [string]$viText) { if ($vi) { $viText } else { $en } }
function Fail([string]$en, [string]$viText) {
  [void][System.Windows.Forms.MessageBox]::Show((T $en $viText), 'HNMO', 'OK', 'Error')
  exit 1
}

# The ports from the host settings (save\hnmo-settings.json), else the defaults.
$webPort = 8080; $adminPort = 8095
$settingsFile = Join-Path $root 'save\hnmo-settings.json'
if (Test-Path $settingsFile) {
  try {
    $settings = Get-Content $settingsFile -Raw | ConvertFrom-Json
    if ($settings.WebPort) { $webPort = [int]$settings.WebPort }
    if ($settings.AdminPort) { $adminPort = [int]$settings.AdminPort }
  } catch { }
}

# The launcher: the folder's own HNMO.exe, else the portable runtime with HNMO.dll.
$web = Join-Path $root 'web'
$exe = Join-Path $web 'HNMO.exe'
$dotnet = Join-Path $root 'runtime\win-x64\dotnet.exe'
try {
  if (Test-Path $exe) {
    $process = Start-Process -FilePath $exe -WorkingDirectory $web -WindowStyle Hidden -PassThru
  } elseif (Test-Path $dotnet) {
    $process = Start-Process -FilePath $dotnet -ArgumentList 'HNMO.dll' -WorkingDirectory $web -WindowStyle Hidden -PassThru
  } else {
    Fail 'HNMO can''t find its program (web\HNMO.exe or runtime\win-x64). Extract HNMO.zip again.' 'HNMO không tìm thấy chương trình (web\HNMO.exe hoặc runtime\win-x64). Hãy giải nén lại HNMO.zip.'
  }
} catch {
  Fail "HNMO couldn't start: $($_.Exception.Message)" "HNMO không khởi động được: $($_.Exception.Message)"
}
# Keeping the handle lets ExitCode be read once the launcher ends (a Start-Process quirk).
$null = $process.Handle

$icon = New-Object System.Windows.Forms.NotifyIcon
try { $icon.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon($(if (Test-Path $exe) { $exe } else { $dotnet })) } catch { $icon.Icon = [System.Drawing.SystemIcons]::Application }
$icon.Text = 'HNMO'
$menu = New-Object System.Windows.Forms.ContextMenuStrip
$script:stopping = $false
function Open-Url([string]$url) { Start-Process $url }
[void]$menu.Items.Add((T 'Open the game' 'Mở game'), $null, { Open-Url "http://localhost:$webPort/" })
[void]$menu.Items.Add((T 'Open the admin panel' 'Mở admin panel'), $null, { Open-Url "http://localhost:$adminPort/" })
[void]$menu.Items.Add((T 'Show the log' 'Xem nhật ký'), $null, { Open-Url "http://localhost:$adminPort/server-log" })
[void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
[void]$menu.Items.Add((T 'Stop HNMO' 'Tắt HNMO'), $null, {
  $answer = [System.Windows.Forms.MessageBox]::Show((T 'Stop the game for everyone?' 'Tắt game cho mọi người?'), 'HNMO', 'YesNo', 'Question')
  if ($answer -ne 'Yes') { return }
  $script:stopping = $true
  $icon.Text = (T 'HNMO is stopping...' 'HNMO đang tắt...')
  try { Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$webPort/api/control/shutdown" -TimeoutSec 10 | Out-Null }
  catch { try { $process.Kill() } catch { } }
})
$icon.ContextMenuStrip = $menu
$icon.add_DoubleClick({ Open-Url "http://localhost:$webPort/" })
$icon.Visible = $true
$icon.ShowBalloonTip(5000, 'HNMO', (T 'HNMO is starting. The game opens in your browser; right-click this icon for the admin panel, the log or to stop.' 'HNMO đang khởi động. Game sẽ mở trong trình duyệt; bấm chuột phải vào biểu tượng này để mở admin, xem nhật ký hoặc tắt.'), 'Info')

# Watches the launcher: when it ends, the icon goes; an unexpected end is reported.
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 2000
$timer.add_Tick({
  if (-not $process.HasExited) { return }
  $timer.Stop()
  $icon.Visible = $false
  $icon.Dispose()
  if (-not $script:stopping -and $process.ExitCode -ne 0) {
    $log = Join-Path $root 'save\logs\launcher.log'
    $answer = [System.Windows.Forms.MessageBox]::Show((T "HNMO stopped by itself (code $($process.ExitCode)). Open the log to see why?" "HNMO tự dừng (mã $($process.ExitCode)). Mở nhật ký để xem lý do?"), 'HNMO', 'YesNo', 'Warning')
    if ($answer -eq 'Yes' -and (Test-Path $log)) { Start-Process notepad.exe $log }
  }
  [System.Windows.Forms.Application]::Exit()
})
$timer.Start()
[System.Windows.Forms.Application]::Run()
