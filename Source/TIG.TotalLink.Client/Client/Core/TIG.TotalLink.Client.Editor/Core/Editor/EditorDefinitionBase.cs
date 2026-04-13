using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Editor.Core.Editor
{
    public abstract class EditorDefinitionBase : BindableBase
    {
        #region Private Fields

        private EditorWrapperBase _wrapper;

        #endregion


        #region Public Properties

        /// <summary>
        /// The parent EditorWrapperBase that contains this EditorDefinitionBase.
        /// </summary>
        public virtual EditorWrapperBase Wrapper
        {
            get { return _wrapper; }
            set { SetProperty(ref _wrapper, value, () => Wrapper); }
        }

        /// <summary>
        /// The default width for grid columns that use this editor.
        /// </summary>
        public virtual double DefaultColumnWidth
        {
            get { return double.NaN; }
        }

        /// <summary>
        /// The default FixedWidth setting for grid columns that use this editor.
        /// </summary>
        public virtual bool DefaultFixedWidth
        {
            get { return false; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Determines if the supplied value can successfully be stored in this editor.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>The error message if any error was found; otherwise null.</returns>
        public virtual string Validate(object value)
        {
            return null;
        }

        #endregion
    }
}
