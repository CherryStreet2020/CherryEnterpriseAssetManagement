#!/bin/bash
set -e

# Restore .NET dependencies after a task merge so the project is buildable.
dotnet restore Abs.FixedAssets.csproj
