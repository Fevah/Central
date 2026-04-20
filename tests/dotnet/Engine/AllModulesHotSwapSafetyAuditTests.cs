using System.IO;

namespace Central.Tests.Engine;

/// <summary>
/// Phase 3 hot-swap safety audit — lint that every module's source
/// code is free of WPF patterns that root the
/// <see cref="System.Runtime.Loader.AssemblyLoadContext"/> and
/// prevent collection on unload.
///
/// See <c>docs/MODULE_UPDATE_SYSTEM.md</c> + the class XML on
/// <c>CollectibleModuleLoadContext</c> for the full list of rooting
/// patterns. This test scans module source text for the two most
/// common ones:
///
/// <list type="number">
/// <item><b><c>EventManager.RegisterClassHandler</c></b>. The
/// subscription is rooted on the module's handler type (static from
/// the AppDomain's point of view), so nothing from the module's ALC
/// can ever be collected. The safe alternative is per-instance event
/// subscription inside a panel's code-behind (disposed on
/// <see cref="Central.Engine.Shell.ModuleReloadingMessage"/>).</item>
///
/// <item><b><c>Application.Current.Resources.MergedDictionaries.Add</c></b>.
/// WPF holds strong typed references to every dictionary merged at
/// the app level, including the types declared by the module's
/// themes + styles. When the module unloads, those dictionaries
/// survive + keep the module's types alive. The safe alternative is
/// dictionary-per-UserControl (already required by
/// <c>feedback_wpf_resources</c>).</item>
/// </list>
///
/// When this lint fails, the fix is usually obvious from the failure
/// message — but the REAL cost of letting it ship is that hot-swap
/// silently leaks every module reload until the process exits. Each
/// leaked ALC holds its DLL + all its types' static data in memory.
/// After ~50 reloads a long-running shell can be 500MB heavier with
/// no visible trigger.
/// </summary>
[Collection("RibbonAudit")]
public class AllModulesHotSwapSafetyAuditTests
{
    /// <summary>
    /// Locate the repo root by walking up from the test binary's
    /// directory until we hit a folder containing
    /// <c>Central.sln</c>. Test output typically lives at
    /// <c>tests/dotnet/bin/Debug/net10.0-windows/</c>, so four or
    /// five hops normally.
    /// </summary>
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Central.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// The two forbidden-in-module patterns. Plain substring match
    /// is enough because these APIs are fully-qualified nearly
    /// everywhere + a false positive on a comment is easy to
    /// suppress by reshaping the comment (or moving the pattern
    /// out of the module layer).
    /// </summary>
    private static readonly (string Pattern, string Reason)[] ForbiddenPatterns = new[]
    {
        ("EventManager.RegisterClassHandler",
         "registers a static handler on the WPF class — roots the " +
         "module's ALC + prevents Unload collection. Use per-instance " +
         "event subscription in a panel's code-behind + dispose on " +
         "ModuleReloadingMessage instead."),
        ("Application.Current.Resources.MergedDictionaries.Add",
         "merges a ResourceDictionary into the app-global scope — " +
         "WPF holds strong typed refs that keep the module's types " +
         "alive post-unload. Keep resources inside the UserControl's " +
         "own Resources block (see feedback_wpf_resources)."),
    };

    [Fact]
    public void NoModuleSourceContainsGcRootingWpfPatterns()
    {
        var repo = FindRepoRoot();
        Assert.NotNull(repo); // test infrastructure problem, not a module problem

        var modulesDir = Path.Combine(repo!, "modules");
        Assert.True(Directory.Exists(modulesDir),
            $"modules/ directory not found at {modulesDir}. Test may need " +
            $"updating if the repo layout changed.");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(modulesDir, "*.cs", SearchOption.AllDirectories))
        {
            // Skip generated bin/obj output accidentally captured in the walk.
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }

            foreach (var (pattern, reason) in ForbiddenPatterns)
            {
                if (text.Contains(pattern, StringComparison.Ordinal))
                {
                    var rel = Path.GetRelativePath(repo!, file);
                    offenders.Add($"  {rel}: found '{pattern}' — {reason}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Module source contains WPF patterns that root the AssemblyLoadContext " +
            "and prevent Phase 3 hot-swap collection:\n" +
            string.Join("\n", offenders));
    }
}
