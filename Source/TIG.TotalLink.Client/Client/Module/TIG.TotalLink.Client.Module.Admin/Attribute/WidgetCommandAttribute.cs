using System;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Attribute
{
    /// <summary>
    /// Defines a property as a widget command.
    /// Should be applied to properties that return an ICommand, and are within a class which inherits from WidgetViewModelBase.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class WidgetCommandAttribute : System.Attribute
    {
        #region Constructors

        public WidgetCommandAttribute(string name, string groupName, RibbonItemType itemType, string description)
        {
            Name = name;
            GroupName = groupName;
            RibbonItemType = itemType;
            Description = description;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Name of the command.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Name of the group that the command will be contained in.
        /// </summary>
        public string GroupName { get; private set; }

        /// <summary>
        /// The type of bar item that will be used to represent the command.
        /// </summary>
        public RibbonItemType RibbonItemType { get; private set; }

        /// <summary>
        /// Short description of the command.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Parameter value that will be passed to the command that is bound to this button.
        /// </summary>
        public object CommandParameter { get; set; }

        #endregion
    }
}
