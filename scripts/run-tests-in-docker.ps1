#!/usr/bin/env pwsh

# Create logs directory if it doesn't exist
$logsDir = Join-Path $PSScriptRoot ".." "logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

# Create a timestamped log filename
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $logsDir "docker-test-output_$timestamp.log"

# Build the test image
Write-Host "Building test image..." -ForegroundColor Cyan
docker-compose -f docker/docker-compose.yml build --no-cache simplefiletransfer.tests

# Run the tests in a container and capture the output
Write-Host "Running tests in Docker container..." -ForegroundColor Cyan
Write-Host "Log will be saved to: $logFile" -ForegroundColor Cyan
docker run --rm simplefiletransfertests:latest | Tee-Object -FilePath $logFile

Write-Host "Tests completed." -ForegroundColor Green 