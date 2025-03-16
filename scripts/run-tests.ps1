#!/usr/bin/env pwsh

# Set a timeout for the test process
$timeout = 120 # seconds

# Create logs directory if it doesn't exist
$logsDir = Join-Path $PSScriptRoot ".." "logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

# Create a timestamped log filename
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $logsDir "test-output_$timestamp.log"

Write-Host "Running tests with a $timeout second timeout..." -ForegroundColor Cyan
Write-Host "Log will be saved to: $logFile" -ForegroundColor Cyan

# Start the test process and capture its output
$process = Start-Process -FilePath "dotnet" -ArgumentList "test", "./SimpleFileTransfer.sln", "-v", "d" -PassThru -NoNewWindow -RedirectStandardOutput $logFile

# Wait for the process to complete or timeout
$completed = $process.WaitForExit($timeout * 1000)

if (-not $completed) {
    Write-Host "Tests timed out after $timeout seconds. Killing the process..." -ForegroundColor Red
    $process.Kill()
    
    # Display the last few lines of output
    if (Test-Path $logFile) {
        Write-Host "Last output from tests:" -ForegroundColor Yellow
        Get-Content $logFile -Tail 20
    }
    
    exit 1
} else {
    # Display the test results
    if (Test-Path $logFile) {
        Get-Content $logFile
    }
    
    if ($process.ExitCode -eq 0) {
        Write-Host "Tests completed successfully." -ForegroundColor Green
    } else {
        Write-Host "Tests failed with exit code $($process.ExitCode)." -ForegroundColor Red
    }
    
    exit $process.ExitCode
} 