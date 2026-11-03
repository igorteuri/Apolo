# Abre uma cópia isolada do Apolo (dados numa pasta temporária, nada do usuário é tocado), mostra uma janela
# e salva um print dela. Para conferir o visual durante o desenvolvimento.
#   powershell -ExecutionPolicy Bypass -File tools/preview.ps1 -Janela packs -Saida C:\temp\packs.png [-Tema dark]
# Janelas: packs, credits, prompt, tour, export, bank, missing (ver src/Ares.Player/Player.Preview.cs)
param(
    [Parameter(Mandatory = $true)][string]$Janela,
    [Parameter(Mandatory = $true)][string]$Saida,
    [string]$Exe,
    [int]$Espera = 6,
    [string]$Titulo   # optional: the title of the window to take (when a window opens several, like the tour)
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Exe) { $Exe = Join-Path $root 'src\Ares.Player\bin\Debug\net8.0-windows\Apolo.exe' }
if (-not (Test-Path $Exe)) { throw "Apolo.exe não encontrado: $Exe (compile antes)" }

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class PreviewWin {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    public static string Title(IntPtr h) { var sb = new System.Text.StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int size);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    // visible windows of the process from the top of the screen down (a dialog is above the main window)
    public static List<IntPtr> Visible(uint pid) {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h)) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
'@

$data = Join-Path $env:TEMP ('apolo-preview-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $data | Out-Null
Set-Content -Path (Join-Path $data 'prefs.ini') -Value @('TutorialSeen=1', 'PacksOffered=1') -Encoding UTF8
# default settings already written, as after the first start (else the old "first start" message opens first)
$bin = Split-Path -Parent $Exe
$seed = "`$env:APOLO_DATA_DIR = '$data'; Add-Type -Path '$bin\Ares.Settings.dll'; " +
        "[Ares.Settings.Settings]::Instance.InitializeWithoutSharedMemory(`$null) | Out-Null; " +
        "[Ares.Settings.Settings]::Instance.WriteToFile('$data'); (New-Object Ares.Settings.BasicSettings).WriteToFile()"
& pwsh -NoProfile -Command $seed
$psi = New-Object System.Diagnostics.ProcessStartInfo $Exe
$psi.UseShellExecute = $false
$psi.EnvironmentVariables['APOLO_DATA_DIR'] = $data
$psi.EnvironmentVariables['APOLO_INSTANCE_TAG'] = '-preview'
$psi.EnvironmentVariables['APOLO_PREVIEW'] = $Janela
$psi.EnvironmentVariables['DOTNET_ROLL_FORWARD'] = 'Major'   # the build of development runs on a newer .NET too
$proc = [System.Diagnostics.Process]::Start($psi)
try {
    $found = [IntPtr]::Zero
    $limit = (Get-Date).AddSeconds($Espera + 20)
    while ((Get-Date) -lt $limit -and $found -eq [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 500
        if ($proc.HasExited) { throw 'o Apolo fechou sozinho' }
        # the window opens 5 s after the start (the splash screen is gone by then): main window + the dialog on top
        if (((Get-Date) - $proc.StartTime).TotalSeconds -lt 6) { continue }
        $d = [PreviewWin]::Visible([uint32]$proc.Id)
        if ($Titulo) { foreach ($h in $d) { if ([PreviewWin]::Title($h) -eq $Titulo) { $found = $h } } }
        elseif ($d.Count -ge 2) { $found = $d[0] }
    }
    if ($found -eq [IntPtr]::Zero) { throw "a janela '$Janela' não abriu" }
    Start-Sleep -Milliseconds 1500   # the theme is applied when the program is idle
    $r = New-Object PreviewWin+RECT
    if ([PreviewWin]::DwmGetWindowAttribute($found, 9, [ref]$r, 16) -ne 0) { [PreviewWin]::GetWindowRect($found, [ref]$r) | Out-Null }
    $w = $r.R - $r.L; $h = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    # the content of the window itself (PW_RENDERFULLCONTENT), even when another window is in front of it
    $hdc = $g.GetHdc()
    $ok = [PreviewWin]::PrintWindow($found, $hdc, 2)
    $g.ReleaseHdc($hdc)
    if (-not $ok) { $g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size $w, $h)) }
    $g.Dispose()
    $bmp.Save($Saida, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "OK: $Saida ($w x $h)"
}
finally {
    if (-not $proc.HasExited) { $proc.Kill() }
    Start-Sleep -Milliseconds 500
    Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
}
