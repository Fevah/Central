---
name: Build platform and runtime
description: Must build with -p:Platform=x64, target .NET 10, check crash.log for startup errors
type: feedback
---

Build and runtime requirements for the Central platform.

**Why:** App silently fails without x64 platform flag. Moved from .NET 8 to .NET 10 in March 2026.

**How to apply:**

### Build command
```
dotnet build Central.sln --configuration Release -p:Platform=x64
```

### Target framework
- **All projects target .NET 10** (upgraded March 2026)
- Desktop + modules: `net10.0-windows`
- Core + Data: multi-target `net10.0;net10.0-windows` (API needs plain net10.0)
- API + API.Client: `net10.0`
- Never create new projects targeting net8.0
- Update any hardcoded version references (splash screens, about dialogs, Dockerfiles)

### Crash diagnosis
- Check `crash.log` in the exe directory for startup errors
- `crash.log` is written by App.xaml.cs DispatcherUnhandledException handler
- If app launches but window not visible: saved layout may be off-screen — clear `user_settings WHERE setting_key LIKE 'layout.%'`
- Kill ALL Central.exe processes before rebuild (they lock DLLs)
- Never create test files with `static void Main()` — duplicate entry point fails silently

### DevExpress
- DX 25.2 WPF subscription
- License file: `%AppData%\DevExpress\DevExpress_License.txt`
- Only themes with installed assemblies can be used (filter in PopulateGalleryControl)
