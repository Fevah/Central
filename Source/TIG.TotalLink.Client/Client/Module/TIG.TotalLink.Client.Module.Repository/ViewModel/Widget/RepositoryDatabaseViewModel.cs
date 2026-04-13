using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Module.Global.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Client.Module.Repository.ViewModel.Widget
{
    public class RepositoryDatabaseViewModel : DatabaseConfigViewModelBase
    {
        #region Private Properties

        private readonly IRepositoryFacade _repositoryFacade;

        #endregion


        #region Constructors

        public RepositoryDatabaseViewModel()
        {
        }

        public RepositoryDatabaseViewModel(IGlobalFacade globalFacade, IRepositoryFacade repositoryFacade)
            : base(DatabaseDomain.Repository, globalFacade)
        {
            // Store services
            _repositoryFacade = repositoryFacade;
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Execute method for the TestDatabaseCommand and UpdateDatabaseCommand.
        /// </summary>
        protected override async Task OnUpdateDatabaseExecuteAsync(bool performUpdate)
        {
            try
            {
                await GlobalFacade.UpdateDatabaseAsync(DatabaseDomain, performUpdate);
                await _repositoryFacade.UpdateAllDataStoresAsync(performUpdate);

                if (performUpdate)
                {
                    await GlobalFacade.PopulateDataStoreAsync(DatabaseDomain);
                    await _repositoryFacade.PopulateAllDataStoresAsync();
                    MessageBoxService.Show("Database updated successfully.", "Update Database", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBoxService.Show("Database is up-to-date.", "Test Database", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                var mode = (performUpdate ? "Update" : "Test");
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("{0} database failed!\r\n\r\n{1}", mode, serviceException.Message), string.Format("{0} Database", mode), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Execute method for the PurgeDatabaseCommand.
        /// </summary>
        protected override async Task OnPurgeDatabaseExecuteAsync()
        {
            try
            {
                // Get the results of purging the main Repository database, and all it's data stores
                var results = new List<PurgeResult>();
                results.Add(await GlobalFacade.PurgeDatabaseAsync(DatabaseDomain));
                results.AddRange(await _repositoryFacade.PurgeAllDataStoresAsync());

                // Aggregate the PurgeResults
                var result = new PurgeResult()
                {
                    Processed = results.Sum(p => p.Processed),
                    Purged = results.Sum(p => p.Purged),
                    NonPurged = results.Sum(p => p.NonPurged),
                    ReferencedByNonDeletedObjects = results.Sum(p => p.ReferencedByNonDeletedObjects)
                };

                // Display the result
                MessageBoxService.Show(
                    string.Format(
                        "Purge successful.\r\n\r\nProcessed: {0}\r\nPurged: {1}\r\nNon Purged: {2}\r\nReferenced By Non Deleted Object: {3}",
                        result.Processed, result.Purged, result.NonPurged, result.ReferencedByNonDeletedObjects),
                    "Purge Deleted Objects", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("Purge failed!\r\n\r\n{0}", serviceException.Message), "Purge Database", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                ConnectToFacade(_repositoryFacade);
            });
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<RepositoryDatabaseViewModel> builder)
        {
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<RepositoryDatabaseViewModel> builder)
        {
        }

        #endregion
    }
}
