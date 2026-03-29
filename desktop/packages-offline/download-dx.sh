#!/bin/bash
# Download all DevExpress WPF 25.2.5 packages for offline use
FEED="https://nuget.devexpress.com/PiFdftcllcpsjvcoYmxRugJvy9BTtRkvEHgQbCd11DLSCOpRG3/api/v3/package"
VER="25.2.5"

PACKAGES=(
  # Core / infrastructure
  DevExpress.Wpf
  DevExpress.Wpf.Core
  DevExpress.Wpf.Core.Extensions
  DevExpress.Wpf.Controls
  DevExpress.Wpf.ExpressionEditor
  DevExpress.Wpf.LayoutControl
  DevExpress.Wpf.Printing
  DevExpress.Wpf.Office
  DevExpress.Wpf.Dialogs
  DevExpress.Wpf.TypedStyles
  DevExpress.Wpf.PrismAdapters
  # Grid
  DevExpress.Wpf.Grid
  DevExpress.Wpf.Grid.Core
  DevExpress.Wpf.Grid.Printing
  # Ribbon / Bars
  DevExpress.Wpf.Ribbon
  # Docking
  DevExpress.Wpf.Docking
  # Charts / Gauges / Maps / TreeMap
  DevExpress.Wpf.Charts
  DevExpress.Wpf.Gauges
  DevExpress.Wpf.Map
  DevExpress.Wpf.TreeMap
  # Diagram
  DevExpress.Wpf.Diagram
  # Scheduler / Scheduling
  DevExpress.Wpf.Scheduler
  DevExpress.Wpf.Scheduling
  DevExpress.Wpf.SchedulingReporting
  # Gantt
  DevExpress.Wpf.Gantt
  # Navigation
  DevExpress.Wpf.Accordion
  DevExpress.Wpf.Carousel
  DevExpress.Wpf.NavBar
  # Property Grid
  DevExpress.Wpf.PropertyGrid
  # Pivot
  DevExpress.Wpf.PivotGrid
  # Rich Edit / Spreadsheet / PDF / SpellChecker
  DevExpress.Wpf.RichEdit
  DevExpress.Wpf.Spreadsheet
  DevExpress.Wpf.PdfViewer
  DevExpress.Wpf.SpellChecker
  # Reporting / Dashboard
  DevExpress.Wpf.Reporting
  DevExpress.Wpf.Dashboard
  DevExpress.Wpf.DocumentViewer.Core
  # Themes (installed + useful)
  DevExpress.Wpf.Themes.All
  DevExpress.Wpf.Themes.Office2019Colorful
  DevExpress.Wpf.Themes.Office2019DarkGray
  DevExpress.Wpf.Themes.Office2019Black
  DevExpress.Wpf.Themes.Office2019White
  DevExpress.Wpf.Themes.Office2019HighContrast
  DevExpress.Wpf.Themes.Win11Dark
  DevExpress.Wpf.Themes.Win11Light
  DevExpress.Wpf.Themes.Win10Dark
  DevExpress.Wpf.Themes.Win10Light
  DevExpress.Wpf.Themes.VS2019Dark
  DevExpress.Wpf.Themes.VS2019Light
  DevExpress.Wpf.Themes.VS2019Blue
  DevExpress.Wpf.Themes.VS2017Dark
  DevExpress.Wpf.Themes.VS2017Light
  DevExpress.Wpf.Themes.VS2017Blue
  DevExpress.Wpf.ThemesLW
  DevExpress.Wpf.Themes.DXStyle
)

echo "Downloading ${#PACKAGES[@]} DevExpress WPF packages v${VER}..."
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
