using System.Collections.ObjectModel;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Command;
using TIG.TotalLink.Client.Module.Admin.Provider;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage
{
    public class WidgetCardViewModel : ViewModelBase
    {
        #region Private Fields

        private readonly IWidgetProvider _widgetProvider;
        private readonly ShowDocumentCommand _showDocumentCommand;
        private WidgetViewModel _widget;

        #endregion


        #region Constructors

        public WidgetCardViewModel()
        {
        }

        public WidgetCardViewModel(IWidgetProvider widgetProvider)
            : this()
        {
            // Store services
            _widgetProvider = widgetProvider;

            // Initialize commands
            _showDocumentCommand = new ShowDocumentCommand();
            SelectionChangedCommand = new DelegateCommand(OnSelectionChangedExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// The commmand of selection changed.
        /// </summary>
        public ICommand SelectionChangedCommand { get; set; }

        #endregion
        

        #region Public Properties

        /// <summary>
        /// The selected widget.
        /// </summary>
        public WidgetViewModel Widget
        {
            get { return _widget; }
            set { SetProperty(ref _widget, value, () => Widget); }
        }

        /// <summary>
        /// A list of all available widgets plus a blank document.
        /// </summary>
        public ObservableCollection<WidgetViewModel> Widgets
        {
            get
            {
                // Return null if widget provider is null
                if (_widgetProvider == null)
                {
                    return null;
                }

                return _widgetProvider.Widgets;
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the SelectionChangedCommand.
        /// </summary>
        private void OnSelectionChangedExecute()
        {
            if (Widget == null)
            {
                return;
            }
            _showDocumentCommand.Execute(Widget);
            Widget = null;
        }

        #endregion
    }
}
