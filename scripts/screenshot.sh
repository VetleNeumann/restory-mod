#!/usr/bin/env bash
# Capture the Windows primary screen at native resolution (DPI-aware) from WSL.
# Usage: scripts/screenshot.sh [output.png]   (default: screenshots/<timestamp>.png, gitignored)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/screenshots/$(date +%Y%m%d-%H%M%S).png}"
mkdir -p "$(dirname "$OUT")" /mnt/c/tmp
WIN_TMP='C:\tmp\restory-screenshot.png'

/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -Command "
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace Win32 -Name Dpi -MemberDefinition '[DllImport(\"user32.dll\")] public static extern bool SetProcessDPIAware();'
[void][Win32.Dpi]::SetProcessDPIAware()
\$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
\$bmp = New-Object System.Drawing.Bitmap \$b.Width, \$b.Height
\$g = [System.Drawing.Graphics]::FromImage(\$bmp)
\$g.CopyFromScreen(\$b.Location, [System.Drawing.Point]::Empty, \$b.Size)
\$bmp.Save('$WIN_TMP')
Write-Output (\"{0}x{1}\" -f \$b.Width, \$b.Height)
"
mv /mnt/c/tmp/restory-screenshot.png "$OUT"
echo "$OUT"
