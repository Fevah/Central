using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Attribute
{
    /// <summary>
    /// Defines the default size for dialog windows which display a data object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DialogSizeAttribute : System.Attribute
    {
        #region Constructors

        public DialogSizeAttribute(double defaultWidth, double defaultHeight)
        {
            DefaultWidth = defaultWidth;
            DefaultHeight = defaultHeight;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The default width for the window.
        /// </summary>
        public double DefaultWidth { get; private set; }

        /// <summary>
        /// The default height for the window.
        /// </summary>
        public double DefaultHeight { get; private set; }

        #endregion
    }
}
