using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class ButtonEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private string _buttonText;

        #endregion


        #region Public Properties

        /// <summary>
        /// The text to display on the button.
        /// If this value is null, the DisplayName will be used instead.
        /// </summary>
        public string ButtonText
        {
            get { return _buttonText; }
            set { SetProperty(ref _buttonText, value, () => ButtonText, () => RaisePropertyChanged(() => ActualButtonText)); }
        }

        /// <summary>
        /// The actual text to display on the button.
        /// ButtonText will be returned if it is nt null, otherwise DisplayName will be returned.
        /// </summary>
        public string ActualButtonText
        {
            get { return (ButtonText ?? Wrapper.DisplayName); }
        }

        #endregion
    }
}
