#!/bin/bash
set -e
cd /Users/deandunagan/Documents/Claude/Projects/EnterpriseAssetManagament/CherryEnterpriseAssetManagement
export PATH="$HOME/.dotnet:$PATH"
echo "=== B11 R2-6 migration add starting $(date) ==="
$HOME/.dotnet/dotnet tool run dotnet-ef migrations add B11_R2_6_ResourceCalendarsFiniteCapacity --output-dir Migrations
echo "=== EXIT $? $(date) ==="
