using System.Windows.Input;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Item
{
    public class BackstageButtonItemViewModel : BackstageItemViewModelBase
    {
        #region Private Fields

        private ICommand _command;
        private object _commandParameter;

        #endregion


        #region Constructors

        public BackstageButtonItemViewModel()
        {
        }

        public BackstageButtonItemViewModel(string name, ICommand command)
            : base(name)
        {
            _command = command;
        }

        public BackstageButtonItemViewModel(string name, ICommand command, object commandParameter)
            : this(name, command)
        {
            _commandParameter = commandParameter;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Command that will be executed when the button is clicked.
        /// </summary>
        public ICommand Command
        {
            get { return _command; }
            set { SetProperty(ref _command, value, () => Command); }
        }

        /// <summary>
        /// Parameter to be sent to the command.
        /// </summary>
        public object CommandParameter
        {
            get { return _commandParameter; }
            set { SetProperty(ref _commandParameter, value, () => CommandParameter); }
        }

        #endregion

    }
}
