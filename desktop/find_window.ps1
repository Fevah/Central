$p = Get-Process SwitchBuilder -ErrorAction SilentlyContinue
if ($p) {
    Write-Host "Found $($p.Count) process(es)"
    foreach ($proc in $p) {
        $h = $proc.MainWindowHandle
        Write-Host "  PID=$($proc.Id) Handle=$h Title='$($proc.MainWindowTitle)'"
    }
    # Force first one to screen center
    if ($p[0].MainWindowHandle -ne 0) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int h2, bool r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@
        $handle = $p[0].MainWindowHandle
        [Win32]::ShowWindow($handle, 9)  # SW_RESTORE
        [Win32]::MoveWindow($handle, 100, 100, 1440, 900, $true)
        [Win32]::SetForegroundWindow($handle)
        Write-Host "Moved window to (100,100) 1440x900"
    } else {
        Write-Host "MainWindowHandle is 0 - window may not have been created yet"
    }
} else {
    Write-Host "No SwitchBuilder process found"
}
