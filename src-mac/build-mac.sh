#!/bin/sh
set -eu
cd "$(dirname "$0")"
swift build -c release
echo "Built .build/release/K3UsbSafeMac"
