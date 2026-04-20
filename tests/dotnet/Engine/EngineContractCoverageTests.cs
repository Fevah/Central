using Central.Engine.Modules;
using Central.Engine.Shell;

namespace Central.Tests.Engine;

/// <summary>
/// Companion to <see cref="EngineContractBaselineTests"/>. The
/// baseline test catches drift in types <i>that are in</i>
/// <see cref="EngineContractSnapshot.GetContractTypes"/>. This
/// test catches the opposite failure mode: a new
/// <see cref="IPanelMessage"/> record added to
/// <c>Central.Engine.Shell</c> that <b>isn't</b> in the contract
/// list + silently escapes the drift check.
///
/// <para>When this test fails, add the missing type to the
/// <c>ContractTypes</c> array in <see cref="EngineContractSnapshot"/>
/// AND regenerate the baseline (<c>ENGINE_CONTRACT_REGENERATE=1</c>)
/// in the same PR that adds the new message.</para>
/// </summary>
[Collection("RibbonAudit")]
public class EngineContractCoverageTests
{
    [Fact]
    public void EveryPublicIPanelMessage_IsInContractTypes()
    {
        // Find every public concrete type implementing IPanelMessage
        // in the Central.Engine assembly. Records + classes both
        // qualify; interfaces themselves don't (we cover IPanelMessage
        // via its named inclusion in ContractTypes).
        var messageTypes = typeof(IPanelMessage).Assembly.GetTypes()
            .Where(t => t.IsPublic
                        && !t.IsAbstract
                        && !t.IsInterface
                        && typeof(IPanelMessage).IsAssignableFrom(t))
            .ToList();

        var contract = EngineContractSnapshot.GetContractTypes().ToHashSet();

        var missing = messageTypes
            .Where(t => !contract.Contains(t))
            .Select(t => t.FullName ?? t.Name)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "The following IPanelMessage types exist in Central.Engine but aren't " +
            "listed in EngineContractSnapshot.ContractTypes, so the baseline-drift " +
            "test won't catch signature changes to them:\n  " +
            string.Join("\n  ", missing) + "\n\n" +
            "Add each to the ContractTypes array in " +
            "libs/engine/Modules/EngineContractSnapshot.cs + regenerate the " +
            "baseline via ENGINE_CONTRACT_REGENERATE=1.");
    }

    [Fact]
    public void ContractTypes_AllResolveInCurrentAssembly()
    {
        // Sanity: every type in ContractTypes must itself be loadable
        // (not trimmed, not moved). Guards against a later refactor
        // removing a type but forgetting to update the contract list.
        var types = EngineContractSnapshot.GetContractTypes();
        foreach (var t in types)
        {
            Assert.NotNull(t);
            Assert.NotNull(t.FullName);
            Assert.NotNull(t.Assembly);
        }
    }
}
