using System;

namespace TIG.TotalLink.Client.Module.Admin.Attribute
{
    /// <summary>
    /// Defines a class as a widget.
    /// Should be applied to views who have a related viewmodel which inherits from WidgetViewModelBase.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class WidgetAttribute : System.Attribute
    {
        #region Constructors

        public WidgetAttribute(string name, string category, string description)
        {
            Name = name;
            Category = category;
            Description = description;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Name of the widget.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Category that the widget belongs to.
        /// </summary>
        public string Category { get; private set; }

        /// <summary>
        /// Short description of the widget.
        /// </summary>
        public string Description { get; private set; }
        
        #endregion
    }
}
