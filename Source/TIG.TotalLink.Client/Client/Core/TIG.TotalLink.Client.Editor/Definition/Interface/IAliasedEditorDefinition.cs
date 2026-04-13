using System;
using System.Reflection;

namespace TIG.TotalLink.Client.Editor.Definition.Interface
{
    /// <summary>
    /// When an editor definition uses an alias to display the value from a different field, it should implement this interface
    /// so that inspectors can determine which property contains the real value.
    /// </summary>
    public interface IAliasedEditorDefinition
    {
        #region Public Properties

        /// <summary>
        /// The actual field on the entity that will be used for display.
        /// </summary>
        string ActualDisplayMember { get; }

        /// <summary>
        /// The actual property on the entity that will be used for display.
        /// </summary>
        PropertyInfo ActualDisplayProperty { get; }

        /// <summary>
        /// The type of value that is contained in the ActualDisplayMember.
        /// </summary>
        Type ActualDisplayType { get; }

        #endregion
    }
}
