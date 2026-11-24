# Runs HNMO without a console window and keeps its icon next to the clock.
# Double-click: open the game. Right-click: Open the game / Open the admin panel / Show the log / Stop HNMO.
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

function Get-Ports {
  $wp = 8080; $ap = 8095
  $sf = Join-Path $root 'save\hnmo-settings.json'
  if (Test-Path $sf) {
    try {
      $s = Get-Content $sf -Raw | ConvertFrom-Json
      if ($s.WebPort) { $wp = [int]$s.WebPort }
      if ($s.AdminPort) { $ap = [int]$s.AdminPort }
    } catch { }
  }
  return @{ Web = $wp; Admin = $ap }
}

function Stop-HnmoTree([System.Diagnostics.Process]$proc, [int]$wp) {
  $uri = "http://127.0.0.1:$wp/api/control/shutdown"
  try {
    Invoke-RestMethod -Method Post -Uri $uri -TimeoutSec 6 | Out-Null
    $null = $proc.WaitForExit(8000)
  } catch { }

  if (-not $proc.HasExited) {
    try { & taskkill.exe /F /T /PID $proc.Id 2>$null | Out-Null } catch { }
    try { if (-not $proc.HasExited) { $proc.Kill() } } catch { }
  }

  # Clean up any child game server or PostgreSQL instance left behind if killed mid-startup.
  $serverPidFile = Join-Path $root 'save\server.pid'
  if (Test-Path $serverPidFile) {
    try {
      $parts = (Get-Content $serverPidFile -Raw).Trim() -split '\s+'
      if ($parts.Length -ge 1 -and ($parts[0] -as [int])) {
        Stop-Process -Id ([int]$parts[0]) -Force -ErrorAction SilentlyContinue
      }
      Remove-Item $serverPidFile -Force -ErrorAction SilentlyContinue
    } catch { }
  }
  $pgPidFile = Join-Path $root 'save\db\postmaster.pid'
  if (Test-Path $pgPidFile) {
    $pgCtl = Join-Path $root 'postgres\bin\pg_ctl.exe'
    if (Test-Path $pgCtl) {
      try { & $pgCtl stop -D (Join-Path $root 'save\db') -m fast -w -t 8 2>$null | Out-Null } catch { }
    }
  }
}

$ports = Get-Ports
$webPort = $ports.Web
$adminPort = $ports.Admin

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
[void]$menu.Items.Add((T 'Open the game' 'Mở game'), $null, { $p = (Get-Ports).Web; Open-Url "http://localhost:$p/" })
[void]$menu.Items.Add((T 'Open the admin panel' 'Mở admin panel'), $null, { $p = (Get-Ports).Admin; Open-Url "http://localhost:$p/" })
[void]$menu.Items.Add((T 'Show the log' 'Xem nhật ký'), $null, { $p = (Get-Ports).Admin; Open-Url "http://localhost:$p/server-log" })
[void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
[void]$menu.Items.Add((T 'Stop HNMO' 'Tắt HNMO'), $null, {
  $answer = [System.Windows.Forms.MessageBox]::Show((T 'Stop the game for everyone?' 'Tắt game cho mọi người?'), 'HNMO', 'YesNo', 'Question')
  if ($answer -ne 'Yes') { return }
  $script:stopping = $true
  $icon.Text = (T 'HNMO is stopping...' 'HNMO đang tắt...')
  $p = (Get-Ports).Web
  Stop-HnmoTree $process $p
})
$icon.ContextMenuStrip = $menu
$icon.add_DoubleClick({ $p = (Get-Ports).Web; Open-Url "http://localhost:$p/" })
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
