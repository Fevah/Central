using System.Windows.Input;

namespace Central.Engine.Widgets;

/// <summary>
/// Non-generic interface exposed by ListViewModelBase&lt;T&gt; so
/// GlobalActionService can dispatch commands without knowing the entity type.
/// </summary>
public interface IActionTarget
{
    ICommand? GetActionCommand(string actionKey);
    string TypeNameForCaption => "";
}
