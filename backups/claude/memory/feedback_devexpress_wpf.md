---
name: DevExpress WPF 25.2 patterns
description: Proven DX WPF 25.2 patterns — ItemClick not Command, combo wiring, DockController, backstage, theme gallery, SVG, CheckedListBox, no PageHeaderTemplate, no Ellipsis GlyphKind
type: feedback
---

DevExpress WPF 25.2 has many API differences from documentation. These patterns are proven working.

**Why:** Multiple crashes from wrong DX APIs. These prevent repeating mistakes.

**How to apply:**

### Ribbon & Bars
- `ItemClick` event, NOT `Command` binding on `BarButtonItem`
- `RibbonGalleryBarItem` for galleries, NOT `GalleryControl` inside `PopupMenu`
- `Gallery.ItemClick` fires, NOT `Gallery.ItemChecked`
- DO NOT use `Gallery.ItemCheckMode` — all enum values throw FormatException. Handle in code.
- `BackstageTabItem` uses `Content`, NOT `Header`
- `BackstageButtonItem.Click` is `EventHandler`, NOT `ItemClick`
- `ThemeGalleryItem.Gallery` must be created: `??= new Gallery()`
- Only themes with installed NuGet packages work. Wrap `ThemeManager.ApplicationThemeName` in try/catch.
- **NO `PageHeaderTemplate`** — RibbonPage does not have this property. Use `BarSubItem` in code-behind for page header buttons (e.g., settings cog).
- **NO `Ellipsis` GlyphKind** — `GlyphKind.Ellipsis` does not exist in DX 25.2. Use `GlyphKind.Regular` or set a custom glyph image.
- Context tabs: `RibbonPageCategory` with `Color` property (e.g., Blue, Green, custom brush). Visibility toggles with active panel.
- Quick Access Toolbar: `RibbonControl.ToolbarItems` collection, NOT the deprecated `QuickAccessItems`.

### Grid & Editing
- `NavigationStyle="Cell"` required for inline editing
- `ComboBoxEditSettings` wired in code-behind (XAML binding fails)
- `ShownEditor` wired in constructor, not XAML
- `AutoGenerateColumns="None"` (enum, not bool)
- Saved layouts override XAML AllowEditing
- `LookUpEditSettings` does NOT exist — use `ComboBoxEditSettings` + `ItemTemplate`

### Editors & CheckedListBox
- **`CheckedListBoxEditStyleSettings`** — use for multi-select checkbox lists inside ComboBoxEdit. Set `ItemsSource` + `DisplayMember` + `ValueMember` in code-behind. `EditValue` is a `List<T>` of selected values.
- `CheckedComboBoxStyleSettings` is an ALTERNATIVE but less flexible — use `CheckedListBoxEditStyleSettings` when you need full control over item template.
- Multi-select checkbox behavior: wire `EditValueChanged` event, NOT `SelectedIndexChanged`.

### TreeListControl
- `TreeListControl` uses `KeyFieldName="Id"` + `ParentFieldName="ParentId"` for hierarchy (flat list, NOT HierarchicalDataTemplate)
- `TreeListView` for tree rendering — set `AutoWidth="True"`, `AllowEditing="True"` per column
- `TreeListView.FocusedRowHandle` gives the selected row, use `Tree.GetRow(handle)` to get the data item
- Drag-drop reordering via `TreeListView.AllowDragDrop="True"` — but also provide Move Up/Down buttons for accessibility

### Panels & Docking
- `DockController.Close()/Restore()` for toggle (NOT Visibility.Collapsed)
- Module UserControls can't see Window resources — define styles locally
- `ClosingBehavior="HideToClosedPanelsCollection"` + Close in Loaded

### Layouts
- **Saved layouts override new XAML columns** — always clear layouts after adding new grid columns: `DELETE FROM user_settings WHERE setting_key LIKE 'layout.%'`
- Saved layouts override XAML AllowEditing — skip layout restore for newly-editable grids until user re-saves

### SVG Icons (via Svg.NET, not DX)
- DX `SvgImageSource` does NOT reliably render arbitrary SVG with `currentColor` — use Svg.NET (`SvgDocument.Open` + `.Draw()`) instead
- Convert `System.Drawing.Bitmap` → WPF `BitmapImage` via `MemoryStream` + `BitmapCacheOption.OnLoad` + `Freeze()`
- Replace `currentColor` with `#FFFFFF` before rendering (for dark theme visibility)
- Pre-render SVG→PNG 16px/32px at import time and store both in DB — rendering 11K+ SVGs at runtime is too slow
- `ImagePickerWindow` uses `DXDialogWindow` base class (not regular Window) for DX theme integration
- Set `DecodePixelWidth=32` on `BitmapImage` to limit memory usage when displaying thousands of icons

### Build
- MUST use `-p:Platform=x64` for solution build
- Never create files with duplicate `Main()` entry points
- Check crash.log after launch
- Anonymous types from DB need `Mode=OneWay` binding
- Svg.NET 3.4.7 requires `System.Drawing.Common` — already included via transitive dependency on Windows
