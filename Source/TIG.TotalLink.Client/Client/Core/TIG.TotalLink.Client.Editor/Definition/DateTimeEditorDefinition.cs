using System.Globalization;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class DateTimeEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private bool _showTime;

        #endregion


        #region Constructors

        public DateTimeEditorDefinition()
        {
            LoadCommand = new DelegateCommand<DateEdit>(OnLoadCommandExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// The command to hide the editor button of default DateEdit generated in grid control.
        /// </summary>
        public ICommand LoadCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if the editor should show/edit the time value.
        /// Defaults to false.
        /// </summary>
        public bool ShowTime
        {
            get { return _showTime; }
            set { SetProperty(ref _showTime, value, () => ShowTime); }
        }

        /// <summary>
        /// Returns a date/time mask pattern based on the current culture and the current ShowTime setting.
        /// </summary>
        public string Mask
        {
            get
            {
                if (ShowTime)
                    return string.Format("{0} {1}", CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern, CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern);

                return CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the LoadCommand.
        /// </summary>
        /// <param name="sender">The DateEdit that triggered the command.</param>
        private void OnLoadCommandExecute(DateEdit sender)
        {
            // Abort if the editor is inactive
            if (sender.EditMode != EditMode.InplaceActive)
                return;

            // Find the default parent editor that the grid generated
            var defaultEditor = LayoutHelper.GetParent(sender) as DateEdit;
            if (defaultEditor == null)
                return;

            // Hide the buttons on the default editor
            defaultEditor.ShowEditorButtons = false;
        }

        #endregion


        #region Overrides

        public override double DefaultColumnWidth
        {
            get
            {
                if (ShowTime)
                {
                    return 150d;
                }

                return 100d;
            }
        }

        public override bool DefaultFixedWidth
        {
            get { return true; }
        }

        #endregion
    }
}
