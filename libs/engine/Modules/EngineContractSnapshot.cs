using System.Reflection;
using System.Text;

namespace Central.Engine.Modules;

/// <summary>
/// Phase 6 of the module-update system — the "engine contract"
/// guardrail. Enumerates the public shape of the types that every
/// module depends on (<see cref="IModule"/>,
/// <see cref="IModuleRibbon"/>, <see cref="IModulePanels"/>,
/// <see cref="IPanelBuilder"/>, <see cref="IRibbonBuilder"/>,
/// message-bus record types in <c>Central.Engine.Shell</c>, +
/// <see cref="EngineContract.CurrentVersion"/>) and produces a
/// canonical string.
///
/// <para>The baseline test <c>EngineContractBaselineTest</c>
/// captures the snapshot + compares to a checked-in reference.
/// Any diff fails CI + forces the dev to either revert the change
/// or bump <see cref="EngineContract.CurrentVersion"/> in the same
/// PR that updates the baseline.</para>
///
/// <para><b>Why the snapshot covers <c>CurrentVersion</c> itself:</b>
/// so a bump shows up in the diff. Changing the engine surface
/// without bumping produces a stale baseline; bumping without
/// changing the surface produces a baseline-only diff. Both
/// force a deliberate decision rather than drifting silently.</para>
///
/// <para>Canonical format is one line per declaration, type-first
/// alphabetical, member-name alphabetical within each type. Stable
/// against reordering in source — the snapshot only changes when
/// the real surface changes.</para>
/// </summary>
public static class EngineContractSnapshot
{
    /// <summary>
    /// Types whose public shape constitutes the engine contract.
    /// Adding a type here widens the contract (more things the
    /// snapshot diff detects); removing narrows it. Keep this list
    /// short — the broader the contract, the more accidental diffs
    /// the test catches, which is useful up to a point but becomes
    /// noisy if every internal refactor trips it.
    /// </summary>
    private static readonly Type[] ContractTypes = new[]
    {
        typeof(IModule),
        typeof(IModuleRibbon),
        typeof(IModulePanels),
        typeof(IPanelBuilder),
        typeof(IRibbonBuilder),
        typeof(IRibbonPageBuilder),
        typeof(IRibbonGroupBuilder),
        typeof(IModuleLicenseGate),
        typeof(Shell.IPanelMessage),
        typeof(Shell.NavigateToPanelMessage),
        typeof(Shell.RefreshPanelMessage),
        typeof(Shell.DataModifiedMessage),
        typeof(Shell.SelectionChangedMessage),
        typeof(Shell.LinkSelectionMessage),
        typeof(Shell.OpenPanelMessage),
        typeof(Shell.ModuleReloadingMessage),
        typeof(Shell.ModuleReloadedMessage),
        typeof(Shell.ModuleLoadFailedMessage),
    };

    /// <summary>Compute the canonical snapshot string.</summary>
    public static string Compute()
    {
        var sb = new StringBuilder();

        // Leading line — documents the snapshot format version so a
        // future format tweak (e.g. including attributes) bumps the
        // baseline deliberately rather than silently.
        sb.AppendLine("# Engine contract snapshot v1");
        sb.AppendLine($"EngineContract.CurrentVersion = {EngineContract.CurrentVersion}");
        sb.AppendLine();

        foreach (var type in ContractTypes.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            sb.AppendLine($"== {type.FullName} ==");
            sb.AppendLine($"kind: {TypeKind(type)}");

            // Record types + records-structs surface their positional
            // constructor parameters through properties, so just
            // enumerating public properties + methods is enough
            // without a separate "primary constructor" line.

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                     .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var getter = prop.GetMethod is not null && prop.GetMethod.IsPublic ? "get" : "";
                var setter = prop.SetMethod is not null && prop.SetMethod.IsPublic ? "set" : "";
                var accessors = string.Join("/", new[] { getter, setter }.Where(s => s.Length > 0));
                sb.AppendLine($"prop: {FormatType(prop.PropertyType)} {prop.Name} {{ {accessors} }}");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                        .Where(m => !m.IsSpecialName) // skip property get/set
                                        .OrderBy(m => m.Name, StringComparer.Ordinal)
                                        .ThenBy(m => m.GetParameters().Length))
            {
                var pars = string.Join(", ", method.GetParameters().Select(p =>
                    $"{FormatType(p.ParameterType)} {p.Name}"));
                sb.AppendLine($"method: {FormatType(method.ReturnType)} {method.Name}({pars})");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string TypeKind(Type t)
    {
        if (t.IsInterface) return "interface";
        if (t.IsEnum) return "enum";
        if (t.IsValueType) return "struct";
        // Record detection — records have a compiler-generated
        // Clone method. Records render the same as classes at the
        // reflection level otherwise.
        var isRecord = t.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;
        return isRecord ? "record" : (t.IsAbstract && t.IsSealed ? "static-class" : "class");
    }

    /// <summary>
    /// Canonical type name. Strips the assembly qualifier; keeps the
    /// full namespace. Generics render as <c>Foo&lt;Bar&gt;</c>.
    /// </summary>
    private static string FormatType(Type t)
    {
        if (t.IsGenericType)
        {
            var def  = t.GetGenericTypeDefinition();
            var name = def.FullName ?? def.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];
            var args = string.Join(", ", t.GetGenericArguments().Select(FormatType));
            return $"{name}<{args}>";
        }
        return t.FullName ?? t.Name;
    }
}
