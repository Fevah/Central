using System;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Test;
using TIG.TotalLink.Shared.Facade.Test;

namespace TIG.TotalLink.Client.Module.Test.ViewModel.Widget
{
    public class TestObjectListViewModel : ListViewModelBase<TestObject>
    {
        #region Private Fields

        private readonly ITestFacade _testFacade;

        #endregion


        #region Constructors

        public TestObjectListViewModel()
        {
        }

        public TestObjectListViewModel(ITestFacade testFacade)
            : this()
        {
            // Store services
            _testFacade = testFacade;

            // Initialize commands
            TestCommand = new DelegateCommand(OnTestExecute, OnTestCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to refresh the list.
        /// </summary>
        [WidgetCommand("Test", "Test", RibbonItemType.ButtonItem, "Temporary test command.")]
        public virtual ICommand TestCommand { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the TestCommand.
        /// </summary>
        private void OnTestExecute()
        {
            //_testFacade.ExecuteUpdate(uow =>
            //{
            //    var currentItem = uow.GetObject(CurrentItem);
            //    currentItem.Spin = currentItem.Spin + 1;
            //    currentItem.IncrementingTime = currentItem.IncrementingTime + 1;
            //}, true, true);
        }

        /// <summary>
        /// CanExecute method for the TestCommand.
        /// </summary>
        /// <returns>True if the TestCommand can execute, otherwise false.</returns>
        private bool OnTestCanExecute()
        {
            return (CurrentItem != null);
        }
        
        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_testFacade);

                // Initialize the data source
                ItemsSource = _testFacade.CreateInstantFeedbackSource<TestObject>();
            });
        }

        #endregion
    }
}
