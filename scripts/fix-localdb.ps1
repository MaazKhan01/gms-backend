# Recovers LocalDB from "SQL Server process failed to start".
#
# The failure is always the same: `sqllocaldb stop` reports the instance
# Stopped while an orphaned sqlservr.exe is still holding its master.mdf, so the
# next start cannot open the files. `sqllocaldb info` agrees it is stopped, which
# is what makes it confusing.
#
# It happens when the API process is killed hard while holding connections —
# LocalDB's owner never gets a clean shutdown. Stopping the API gracefully
# (Ctrl+C, or Stop Debugging in Visual Studio) avoids it.
#
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\fix-localdb.ps1

$ErrorActionPreference = 'Continue'
$instance = 'MSSQLLocalDB'

Write-Host "Before:" -ForegroundColor Cyan
sqllocaldb info $instance | Select-String 'State'

# -k kills the process rather than asking it to stop, which is the only thing
# that works once it is wedged.
sqllocaldb stop $instance -k 2>&1 | Out-String | Write-Host
Start-Sleep -Seconds 2

# The step that actually matters: verify it died. It usually has not.
$orphans = Get-Process sqlservr -ErrorAction SilentlyContinue
if ($orphans) {
    foreach ($p in $orphans) {
        Write-Host "Killing orphaned sqlservr PID $($p.Id)" -ForegroundColor Yellow
        Stop-Process -Id $p.Id -Force
    }
    Start-Sleep -Seconds 3
} else {
    Write-Host "No orphaned sqlservr process." -ForegroundColor Green
}

sqllocaldb start $instance 2>&1 | Out-String | Write-Host

Write-Host "After:" -ForegroundColor Cyan
sqllocaldb info $instance | Select-String 'State|Instance pipe'

# Prove it answers, rather than trusting the status line that lied on the way in.
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "olympic-dms" -Q "SET NOCOUNT ON; SELECT COUNT(*) AS Missions FROM Events WHERE IsDeleted = 0;" -W
