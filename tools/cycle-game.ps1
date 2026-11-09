param([switch]$NoBuild, [switch]$NoRestart, [string]$WingspanDir = 'C:\Program Files (x86)\Steam\steamapps\common\Wingspan')
# Development helper: closes Wingspan, builds and deploys the mod, starts the game through Steam,
# waits until the mod announces the first screen and brings the game window to the front.
#   -NoBuild    restart without building
#   -NoRestart  only bring the game window to the front
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$log = Join-Path $WingspanDir 'BepInEx\LogOutput.log'
Add-Type -Namespace WA -Name Win -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
[DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
'@ -ErrorAction SilentlyContinue

function Focus-Game {
    $h = [WA.Win]::FindWindow('UnityWndClass', 'Wingspan')
    if ($h -eq [IntPtr]::Zero) { return $false }
    # A short Alt press lets SetForegroundWindow succeed from a background process.
    [WA.Win]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [WA.Win]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    [void][WA.Win]::ShowWindow($h, 9); [void][WA.Win]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 300
    return ([WA.Win]::GetForegroundWindow() -eq $h)
}

if ($NoRestart) { "focused: $(Focus-Game)"; return }

$p = Get-Process Wingspan -ErrorAction SilentlyContinue
if ($p) {
    $h = [WA.Win]::FindWindow('UnityWndClass', 'Wingspan')
    if ($h -ne [IntPtr]::Zero) { [void][WA.Win]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) }
    if (-not $p.WaitForExit(20000)) { Stop-Process -Id $p.Id -Force; 'forced stop' } else { 'game closed' }
}

if (-not $NoBuild) {
    Push-Location $repo
    $out = dotnet build src/WingspanAccess/WingspanAccess.csproj -c Release -nologo -p:DeployToGame=true "-p:WingspanDir=$WingspanDir" 2>&1
    Pop-Location
    $errors = $out | Select-String ' error ' | Select-Object -First 10
    if ($errors) { 'BUILD FAILED'; $errors | ForEach-Object { $_.Line.Trim() }; return }
    'built and deployed'
}

Remove-Item $log -ErrorAction SilentlyContinue
Start-Process 'steam://rungameid/1054490'
$start = Get-Date
Start-Sleep -Seconds 8
while ($true) {
    $elapsed = ((Get-Date) - $start).TotalSeconds
    if (Test-Path $log) {
        $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($text -match '\[Screen\] top=NewsCenter') { Start-Sleep -Seconds 2; "news screen after $([int]$elapsed)s"; break }
        if ($elapsed -gt 60 -and $text -match '\[Screen\] top=') { "main menu after $([int]$elapsed)s"; break }
    }
    if ($elapsed -gt 240) { 'TIMEOUT'; break }
    Start-Sleep -Seconds 2
}
"focused: $(Focus-Game)"
Get-Content $log | Select-String 'Wingspan Access\].*(Error|Exception)|Tick failed' | Select-Object -First 5 | ForEach-Object { $_.Line }
