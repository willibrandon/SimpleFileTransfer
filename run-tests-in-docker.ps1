#!/usr/bin/env pwsh

# Build the test image
Write-Host "Building test image..." -ForegroundColor Cyan
docker-compose build --no-cache simplefiletransfer.tests

# Run the tests in a container and capture the output
Write-Host "Running tests in Docker container..." -ForegroundColor Cyan
docker run --rm simplefiletransfertests:latest

Write-Host "Tests completed." -ForegroundColor Green 