#!/bin/bash
dotnet test test/SimpleFileTransfer.Tests/SimpleFileTransfer.Tests.csproj --no-build --logger "console;verbosity=normal" -p:ParallelizeTestCollections=false 