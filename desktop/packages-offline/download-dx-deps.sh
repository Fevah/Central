#!/bin/bash
FEED="https://nuget.devexpress.com/PiFdftcllcpsjvcoYmxRugJvy9BTtRkvEHgQbCd11DLSCOpRG3/api/v3/package"
VER="25.2.5"

PACKAGES=(
  DevExpress.Data
  DevExpress.Data.Desktop
  DevExpress.DataAccess
  DevExpress.Drawing
  DevExpress.Images
  DevExpress.Mvvm
  DevExpress.Office.Core
  DevExpress.Pdf.Core
  DevExpress.Pdf.Drawing
  DevExpress.Printing.Core
  DevExpress.RichEdit.Core
  DevExpress.CodeParser
  DevExpress.Xpo
  DevExpress.Scheduler.Core
  DevExpress.Scheduler.CoreDesktop
  DevExpress.Utils
  DevExpress.PivotGrid.Core
  DevExpress.Charts.Core
  DevExpress.Gauges.Core
  DevExpress.Map.Core
  DevExpress.Diagram.Core
  DevExpress.Dashboard.Core
  DevExpress.Reporting.Core
)

echo "Downloading ${#PACKAGES[@]} DevExpress dependency packages v${VER}..."
OKAY=0
FAIL=0
for PKG in "${PACKAGES[@]}"; do
  LOWER=$(echo "$PKG" | tr '[:upper:]' '[:lower:]')
  URL="${FEED}/${LOWER}/${VER}/${LOWER}.${VER}.nupkg"
  FILE="${PKG}.${VER}.nupkg"
  if [ -f "$FILE" ]; then
    echo "  SKIP $PKG (exists)"
    OKAY=$((OKAY+1))
    continue
  fi
  HTTP=$(curl -sL -w "%{http_code}" -o "$FILE" "$URL")
  if [ "$HTTP" = "200" ] && [ -s "$FILE" ]; then
    SIZE=$(stat -c%s "$FILE" 2>/dev/null || stat -f%z "$FILE" 2>/dev/null)
    echo "  OK   $PKG (${SIZE} bytes)"
    OKAY=$((OKAY+1))
  else
    echo "  FAIL $PKG (HTTP $HTTP)"
    rm -f "$FILE"
    FAIL=$((FAIL+1))
  fi
done
echo ""
echo "Done: ${OKAY} downloaded, ${FAIL} failed"
