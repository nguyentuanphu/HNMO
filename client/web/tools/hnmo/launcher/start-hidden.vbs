' Starts HNMO's icon script (hnmo-tray.ps1) without any window: PowerShell itself would show one for a moment.
Set shell = CreateObject("WScript.Shell")
here = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
shell.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File """ & here & "\hnmo-tray.ps1""", 0, False
