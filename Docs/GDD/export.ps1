# Prints the GDD (Yasser_Youssef_GDD.html) to Yasser_Youssef_GDD.pdf with headless Edge; links stay clickable.
# Run after editing the HTML (for example the Prototype Video link and the timestamps):  powershell -File Docs\GDD\export.ps1
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$html = "file:///" + ((Join-Path $here "Yasser_Youssef_GDD.html") -replace '\\', '/' -replace ' ', '%20')
$pdf = Join-Path $here "Yasser_Youssef_GDD.pdf"
$profile = Join-Path $env:TEMP "gdd-edge-profile"
$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
Start-Process -FilePath $edge -Wait -WindowStyle Hidden -ArgumentList @(
    "--headless=new", "--disable-gpu", "--no-pdf-header-footer", "--user-data-dir=`"$profile`"", "--print-to-pdf=`"$pdf`"", "`"$html`"")
Write-Output "Wrote $pdf"
