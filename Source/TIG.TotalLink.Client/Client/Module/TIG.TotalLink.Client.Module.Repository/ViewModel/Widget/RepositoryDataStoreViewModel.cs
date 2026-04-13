using System;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Repository;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Client.Module.Repository.ViewModel.Widget
{
    public class RepositoryDataStoreViewModel : ListViewModelBase<DataStore>
    {
        #region Private Properties

        private readonly IRepositoryFacade _repositoryFacade;

        #endregion


        #region Constructors

        public RepositoryDataStoreViewModel()
        {
        }

        public RepositoryDataStoreViewModel(IRepositoryFacade repositoryFacade)
        {
            // Store services
            _repositoryFacade = repositoryFacade;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the AddCommand.
        /// </summary>
        protected override async Task OnAddExecuteAsync()
        {
            try
            {
                using (var uow = _repositoryFacade.CreateUnitOfWork())
                {
                    var dataStore = new DataStore(uow);
                    if (!DetailDialogService.ShowDialog(DetailEditMode.Add, dataStore))
                    {
                        return;
                    }
                    await _repositoryFacade.CreateRepositoryDataStore(dataStore);
                    uow.StartUiTracking(this, true, false);
                    await uow.CommitChangesAsync();
                }

                // Create Xpo relevant tables.
                await _repositoryFacade.UpdateAllDataStoresAsync(true);
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("Add DataStore failed!\r\n\r\n{0}", serviceException.Message), "Add DataStore", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        /// <summary>
        /// Execute method for the DeleteDataStoreCommand.
        /// </summary>
        protected override async Task OnDeleteExecuteAsync()
        {
            // Show a warning before deleting the items
            if (MessageBoxService.Show(ActionMessageHelper.GetWarningMessage(SelectedItems, "delete"),
                ActionMessageHelper.GetTitle(SelectedItems, "delete"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var lenght = SelectedItems.Count;
                for (var index = 0; index < lenght; index++)
                {
                    var selectedDataStore = SelectedItems[index];
                    try
                    {
                        await _repositoryFacade.DeleteRepositoryDataStore(selectedDataStore);

                        var store = selectedDataStore;
                        await _repositoryFacade.ExecuteUnitOfWorkAsync(uow =>
                        {
                            uow.StartUiTracking(this, true, false);

                            // Get a copy of the data object in this session
                            var sessionDataObject = uow.GetDataObject(store);
                            if (sessionDataObject == null)
                                return;

                            // Delete the object
                            sessionDataObject.Delete();
                        });
                    }
                    catch (Exception ex)
                    {
                        var serviceException = new ServiceExceptionHelper(ex);
                        MessageBoxService.Show(
                            string.Format("Add DataStore failed!\r\n\r\n{0}", serviceException.Message), "Add DataStore",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }


        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the GlobalFacade
                ConnectToFacade(_repositoryFacade);

                // Initialize the data source
                ItemsSource = _repositoryFacade.CreateInstantFeedbackSource<DataStore>();
            });
        }

        #endregion
    }
}
