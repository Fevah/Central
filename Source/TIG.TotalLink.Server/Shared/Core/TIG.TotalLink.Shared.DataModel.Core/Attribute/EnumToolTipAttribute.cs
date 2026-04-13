using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Attribute
{
    /// <summary>
    /// Apply to an enum value to specify a tooltip to display in editors which inherit from EnumEditorDefinitionBase.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnumToolTipAttribute : System.Attribute
    {
        #region Constructors

        public EnumToolTipAttribute(string toolTip)
        {
            ToolTip = toolTip;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The text to display as a tooltip.
        /// </summary>
        public string ToolTip { get; set; }

        #endregion
    }
}
