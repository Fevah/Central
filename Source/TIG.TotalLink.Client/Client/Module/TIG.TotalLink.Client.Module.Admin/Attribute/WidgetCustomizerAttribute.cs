using System;

namespace TIG.TotalLink.Client.Module.Admin.Attribute
{
    /// <summary>
    /// Defines a class as a widget customizer.
    /// Should be applied to classes that inherit from WidgetCustomizerViewModelBase.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class WidgetCustomizerAttribute : System.Attribute
    {
        #region Constructors

        public WidgetCustomizerAttribute(string name, int order)
        {
            Name = name;
            Order = order;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Name of the widget customizer.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Order that this customizer should appear among other customizers.
        /// </summary>
        public int Order { get; private set; }
        
        #endregion
    }
}
