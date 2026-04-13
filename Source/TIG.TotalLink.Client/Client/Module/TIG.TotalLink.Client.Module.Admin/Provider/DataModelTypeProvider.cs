using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.Provider
{
    /// <summary>
    /// Provides lists of various data model types in the loaded assemblies.
    /// </summary>
    public class DataModelTypeProvider : IDataModelTypeProvider
    {
        #region Constructors

        public DataModelTypeProvider()
        {
            // Initialize collections
            DocumentDataModels = new ObservableCollection<DocumentModelTypeViewModel>();
            DataObjectModels = new ObservableCollection<DataObjectTypeViewModel>();
            AllDataModels = new ObservableCollection<DataModelTypeViewModelBase>();

            // Initialize the list of available types
            Task.Run(() => InitializeModelTypes());
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The available document data model types.
        /// </summary>
        public ObservableCollection<DocumentModelTypeViewModel> DocumentDataModels { get; private set; }

        /// <summary>
        /// The available data object model types.
        /// </summary>
        public ObservableCollection<DataObjectTypeViewModel> DataObjectModels { get; private set; }

        /// <summary>
        /// All available data model types.
        /// </summary>
        public ObservableCollection<DataModelTypeViewModelBase> AllDataModels { get; private set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Generates a list of data model types in all loaded assemblies.
        /// </summary>
        private void InitializeModelTypes()
        {
            // Add all types with DocumentDataModelAttributes
            foreach (var type in GetDocumentDataModelTypes())
            {
                var documentModelType = new DocumentModelTypeViewModel(type);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DocumentDataModels.Add(documentModelType);
                    AllDataModels.Add(documentModelType);
                });
            }

            // Add all types which inherit from DataObjectBase or LocalDataObjectBase
            foreach (var type in GetDataObjectTypes())
            {
                var documentModelType = new DataObjectTypeViewModel(type);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DataObjectModels.Add(documentModelType);
                    AllDataModels.Add(documentModelType);
                });
            }
        }

        /// <summary>
        /// Finds all types that have a DocumentDataModelAttribute.
        /// </summary>
        /// <returns>All types that have a DocumentDataModelAttribute.</returns>
        private static IEnumerable<Type> GetDocumentDataModelTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(type => type.GetCustomAttributes(typeof(DocumentDataModelAttribute), true).Length > 0))
                {
                    yield return type;
                }
            }
        }

        /// <summary>
        /// Finds all types which inherit from DataObjectBase or LocalDataObjectBase.
        /// </summary>
        /// <returns>All types which inherit from DataObjectBase or LocalDataObjectBase.</returns>
        private static IEnumerable<Type> GetDataObjectTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && (typeof(DataObjectBase).IsAssignableFrom(type) || typeof(LocalDataObjectBase).IsAssignableFrom(type))))
                {
                    yield return type;
                }
            }
        }

        #endregion
    }
}
