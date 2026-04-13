using System;
using TIG.TotalLink.Client.Module.Admin.Enum;

namespace TIG.TotalLink.Client.Module.Admin.Attribute
{
    /// <summary>
    /// Defines a class which indicate which client need to be hide.
    /// Should be applied to views who have a related viewmodel which inherits from WidgetViewModelBase.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HideWidgetAttribute : System.Attribute
    {
        #region Constructors

        public HideWidgetAttribute(HostTypes hostTypes)
        {
            HostTypes = hostTypes;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// HostTypes to indicate which client need to be hide.
        /// </summary>
        public HostTypes HostTypes { get; private set; }

        #endregion
    }
}