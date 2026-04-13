using System.Collections.ObjectModel;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Provider
{
    public interface IDataModelTypeProvider
    {

        #region Public Properties

        /// <summary>
        /// The available document data model types.
        /// </summary>
        ObservableCollection<DocumentModelTypeViewModel> DocumentDataModels { get; }

        /// <summary>
        /// The available data object model types.
        /// </summary>
        ObservableCollection<DataObjectTypeViewModel> DataObjectModels { get; }

        /// <summary>
        /// All available data model types.
        /// </summary>
        ObservableCollection<DataModelTypeViewModelBase> AllDataModels { get; }

        #endregion
    }
}
