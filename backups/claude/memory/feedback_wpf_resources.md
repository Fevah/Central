---
name: WPF UserControl resource isolation — causes silent layout deadlock
description: CRITICAL — Module UserControls cannot see Window resources. Missing converters cause infinite layout loop (hang, not crash). Define all resources locally.
type: feedback
---

Module UserControls (in Central.Module.* projects) **cannot access resources defined in MainWindow.xaml**. WPF's resource lookup only walks the logical tree — UserControls embedded in DockPanels are not in the Window's logical tree during first layout.

**Why:** On 2026-03-27, adding `{StaticResource StringToBrush}` in `DeviceGridPanel.xaml` (Module.Devices) caused the app to hang infinitely on startup. The converter was defined in MainWindow.xaml but the module couldn't find it. WPF threw `XamlParseException: Cannot find resource named 'StringToBrush'` during MeasureOverride, which caused an infinite layout loop. The app froze at `mainWindow.Show()` with no crash log, no error message — just a permanent hang. Took hours to diagnose.

**How to apply:**
1. Every module UserControl that uses a converter/style MUST define it in `<UserControl.Resources>`:
   ```xml
   <UserControl.Resources>
       <conv:StringToBrushConverter x:Key="StringToBrush"
           xmlns:conv="clr-namespace:Central.Core.Converters;assembly=Central.Core" />
   </UserControl.Resources>
   ```
2. NEVER rely on `{StaticResource ...}` resolving from the parent Window
3. `{DynamicResource ...}` also won't work at measure time
4. When adding ANY binding with a converter to a module panel, check the panel's local resources FIRST
5. The global `DispatcherUnhandledException` now catches `XamlParseException` and recovers instead of deadlocking
6. `XamlResourceValidator` can check panels at startup — logs warnings to startup.log
7. After extracting or modifying module panel XAML, always grep for `StaticResource` and verify each one has a local definition
