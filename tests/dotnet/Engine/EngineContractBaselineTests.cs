using System.IO;
using Central.Engine.Modules;

namespace Central.Tests.Engine;

/// <summary>
/// Phase 6 guardrail — asserts the engine contract snapshot matches
/// the checked-in baseline <c>engine-contract-v1.txt</c>. Any
/// intentional change to the engine surface requires regenerating
/// the baseline + bumping <see cref="EngineContract.CurrentVersion"/>
/// in the same PR (the snapshot captures CurrentVersion, so the
/// two are coupled — you can't bump one without touching the other).
///
/// <para>First-run UX: when the baseline file is missing, the test
/// writes the current snapshot + fails with instructions to commit
/// the file. Subsequent runs read + compare normally.</para>
///
/// <para>Regenerating intentionally: delete
/// <c>tests/dotnet/Engine/engine-contract-v1.txt</c>, run the test
/// once, inspect the regenerated file, + commit. OR set the
/// <c>ENGINE_CONTRACT_REGENERATE</c> environment variable and run
/// the test — it overwrites the file without failing.</para>
/// </summary>
[Collection("RibbonAudit")]
public class EngineContractBaselineTests
{
    /// <summary>
    /// Walk up from the test binary directory to find the repo root
    /// (containing <c>Central.sln</c>). Same mechanism as
    /// <see cref="AllModulesHotSwapSafetyAuditTests"/>.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Central.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Couldn't find Central.sln above the test binary. " +
            "Test infrastructure issue, not an engine-contract issue.");
    }

    private static string BaselinePath()
        => Path.Combine(FindRepoRoot(), "tests", "dotnet", "Engine", "engine-contract-v1.txt");

    [Fact]
    public void SnapshotMatchesCheckedInBaseline()
    {
        var actual   = EngineContractSnapshot.Compute();
        var path     = BaselinePath();
        var regenerate = Environment.GetEnvironmentVariable("ENGINE_CONTRACT_REGENERATE") == "1";

        if (regenerate)
        {
            File.WriteAllText(path, actual);
            return;
        }

        if (!File.Exists(path))
        {
            File.WriteAllText(path, actual);
            Assert.Fail(
                $"Engine-contract baseline created at {path}. " +
                "Commit it to lock in the current surface. " +
                "Subsequent runs will compare against this file.");
        }

        var expected = File.ReadAllText(path);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Assert.Fail(
                "Engine contract has drifted from the checked-in baseline.\n\n" +
                "If the change is INTENTIONAL:\n" +
                "  1. Bump EngineContract.CurrentVersion in libs/engine/Modules/IModule.cs\n" +
                "  2. Regenerate the baseline — set ENGINE_CONTRACT_REGENERATE=1 and re-run this test, " +
                     "OR delete engine-contract-v1.txt and re-run.\n" +
                "  3. Commit both files in the same PR.\n\n" +
                "If the change is UNINTENTIONAL: revert it before merging.\n\n" +
                $"--- expected (baseline) ---\n{expected}\n" +
                $"--- actual (current)   ---\n{actual}");
        }
    }
}
