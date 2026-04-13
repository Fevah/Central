using System.IO;

namespace TIG.TotalLink.Client.Module.Admin.Interface
{
    public delegate Stream GetLayoutDelegate();
    public delegate void SetLayoutDelegate(Stream layout);

    public interface ISupportLayoutData
    {
        #region Public Properties

        /// <summary>
        /// A delegate method which gets a layout from a view as a Stream.
        /// </summary>
        GetLayoutDelegate GetLayout { get; set; }

        /// <summary>
        /// A delegate method which applies a layout Stream to a view.
        /// </summary>
        SetLayoutDelegate SetLayout { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Applies the default layout.
        /// </summary>
        void ApplyDefaultLayout();

        /// <summary>
        /// Applies the last saved layout.
        /// </summary>
        void ApplySavedLayout();

        #endregion
    }
}
