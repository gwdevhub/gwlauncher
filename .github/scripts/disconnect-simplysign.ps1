$ErrorActionPreference = "Stop"

function Get-RunningSimplySignProcess {
    foreach ($process in @(Get-Process -Name "SimplySignDesktop" -ErrorAction SilentlyContinue)) {
        # Windows can still enumerate an exited process while another handle remains open.
        if ($process.HasExited) {
            $process.Dispose()
        } else {
            $process
        }
    }
}

$processes = @(Get-RunningSimplySignProcess)
try {
    foreach ($process in $processes) {
        if (-not $process.HasExited -and $process.CloseMainWindow()) {
            $process.WaitForExit(5000) | Out-Null
        }
    }
} finally {
    foreach ($process in $processes) {
        $process.Dispose()
    }
}

$deadline = [DateTime]::UtcNow.AddSeconds(10)
do {
    $remaining = @(Get-RunningSimplySignProcess)
    if ($remaining.Count -eq 0) {
        Write-Host "SimplySign Desktop disconnected."
        return
    }

    try {
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "SimplySign Desktop is still running after disconnect (PIDs: $($remaining.Id -join ', '))."
        }

        foreach ($process in $remaining) {
            if (-not $process.HasExited) {
                Write-Host "Force-stopping SimplySign Desktop (PID $($process.Id))."
                Stop-Process -InputObject $process -Force
            }
        }
    } finally {
        foreach ($process in $remaining) {
            $process.Dispose()
        }
    }

    Start-Sleep -Milliseconds 250
} while ($true)
