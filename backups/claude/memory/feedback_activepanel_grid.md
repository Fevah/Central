---
name: Every grid must register in ActivePanel + GetActiveGrid
description: CRITICAL — new grid panels must be added to ActivePanel enum, GetActiveGrid switch, and panel activation map or Home tab actions won't work
type: feedback
---

Every new grid panel MUST be registered in 3 places or the Home tab ribbon actions (Print Preview, Column Chooser, Export, Search) won't work on it:

1. `ActivePanel` enum in `MainViewModel.cs`
2. `GetActiveGrid()` switch in `MainWindow.xaml.cs` — return the grid + view
3. Panel activation mapping (the `if (item == XPanel) VM.ActivePanel = ...` block)

**Why:** SD panels were added but missed from all 3. The Home tab Export/Print/Column Chooser buttons silently did nothing when SD grids were focused. Discovered 2026-03-26.

**How to apply:** When creating any new DocumentPanel with a grid, immediately add entries to all 3 locations. TreeListControl panels can't be added (incompatible with GridControl).
