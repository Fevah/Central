using System;
using System.Collections.ObjectModel;
using System.Linq;
using DevExpress.Mvvm;
using Newtonsoft.Json.Linq;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class WidgetModelInitData : BindableBase
    {
        #region Private Fields

        private readonly DocumentViewModel _parentDocument;

        #endregion


        #region Constructors

        public WidgetModelInitData(DocumentViewModel parentDocument)
        {
            _parentDocument = parentDocument;

            // Initialize collections
            ModelInitializers = new ObservableCollection<WidgetModelInit>();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of model initializers for this widget.
        /// </summary>
        public ObservableCollection<WidgetModelInit> ModelInitializers { get; private set; }

        /// <summary>
        /// Indicates if this data contains any model initializers.
        /// </summary>
        public bool HasModelInitializers
        {
            get
            {
                Clean();
                return ModelInitializers.Count > 0;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns a WidgetModelInit for the specified type.
        /// </summary>
        /// <returns></returns>
        public WidgetModelInit GetModelInitializer(Type type)
        {
            // Attempt to find an existing model initializer for the type
            var modelInit = ModelInitializers.FirstOrDefault(i => i.DataModelType == type);
            if (modelInit != null)
                return modelInit;

            // If no model initializer was found, create and return a new one
            modelInit = new WidgetModelInit(type, _parentDocument);
            ModelInitializers.Add(modelInit);
            return modelInit;
        }

        /// <summary>
        /// Returns a JObject which represents this init data.
        /// </summary>
        /// <returns>A JObject which represents this init data.</returns>
        public JObject SerializeToJsonObject()
        {
            Clean();

            // Create a json object that represents this init data
            return new JObject(
                new JProperty("Type", "InitData"),
                new JProperty("ModelInitializers",
                    new JArray(
                        ModelInitializers.Select(
                            i => new JObject(
                                new JProperty("DataModelType", i.DataModelType.FullName),
                                new JProperty("InitMode", i.InitMode),
                                new JProperty("ChildPropertyName", i.ChildPropertyName)
                            )
                        )
                    )
                )
            );
        }

        /// <summary>
        /// Deserializes this init data from a JObject.
        /// </summary>
        /// <param name="jObject">The JObject to deserialize from.</param>
        public void DeserializeFromJsonObject(JObject jObject)
        {
            // Abort if the jobject is null
            if (jObject == null)
                return;

            // Parse the ModelInitializers array
            ModelInitializers.Clear();
            foreach (var jsonInit in jObject["ModelInitializers"].Children().Cast<JObject>())
            {
                // Attempt to get the DataModelType and abort if it is not found
                var dataModelTypeString = (string)jsonInit["DataModelType"];
                var dataModelType = DataModelHelper.GetTypeFromLoadedAssemblies(dataModelTypeString);
                if (dataModelType == null)
                    continue;

                // Create a new WidgetModelInit and add it to the ModelInitializers
                ModelInitializers.Add(new WidgetModelInit(dataModelType, _parentDocument)
                {
                    InitMode = (WidgetModelInit.InitModes)((int)jsonInit["InitMode"]),
                    ChildPropertyName = (string)jsonInit["ChildPropertyName"]
                });
            }
        }

        /// <summary>
        /// Cleans the widget data by removing any model inits that are empty.
        /// </summary>
        public void Clean()
        {
            foreach (var emptyInit in ModelInitializers.Where(i => i.IsEmpty).ToList())
            {
                ModelInitializers.Remove(emptyInit);
            }
        }

        #endregion
    }
}
