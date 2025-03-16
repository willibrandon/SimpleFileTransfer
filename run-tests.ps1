#!/usr/bin/env pwsh

# Set a timeout for the test process
$timeout = 120 # seconds

Write-Host "Running tests with a $timeout second timeout..." -ForegroundColor Cyan

# Start the test process and capture its output
$process = Start-Process -FilePath "dotnet" -ArgumentList "test", "./SimpleFileTransfer.sln", "-v", "d" -PassThru -NoNewWindow -RedirectStandardOutput "test-output.log"

# Wait for the process to complete or timeout
$completed = $process.WaitForExit($timeout * 1000)

if (-not $completed) {
    Write-Host "Tests timed out after $timeout seconds. Killing the process..." -ForegroundColor Red
    $process.Kill()
    
    # Display the last few lines of output
    if (Test-Path "test-output.log") {
        Write-Host "Last output from tests:" -ForegroundColor Yellow
        Get-Content "test-output.log" -Tail 20
    }
    
    exit 1
} else {
    # Display the test results
    if (Test-Path "test-output.log") {
        Get-Content "test-output.log"
    }
    
    if ($process.ExitCode -eq 0) {
        Write-Host "Tests completed successfully." -ForegroundColor Green
    } else {
        Write-Host "Tests failed with exit code $($process.ExitCode)." -ForegroundColor Red
    }
    
    exit $process.ExitCode
} 