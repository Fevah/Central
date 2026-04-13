using System;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.ServerManager.ViewModel.Widget
{
    public class ServiceConfigViewModel : LocalDetailViewModelBase
    {
        #region Private Fields

        private string _server;
        private int _basePort;

        private readonly IServiceConfiguration _serviceConfiguration;

        #endregion


        #region Constructors

        public ServiceConfigViewModel()
        {
        }

        public ServiceConfigViewModel(IServiceConfiguration serviceConfiguration)
            : this()
        {
            // Store services
            _serviceConfiguration = serviceConfiguration;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the server.
        /// </summary>
        public string Server
        {
            get { return _server; }
            set
            {
                SetProperty(ref _server, value, () => Server, () =>
                {
                    _serviceConfiguration.Server = Server;
                    _serviceConfiguration.Save();
                });
            }
        }

        /// <summary>
        /// The base port for connecting to services.
        /// </summary>
        public int BasePort
        {
            get { return _basePort; }
            set
            {
                SetProperty(ref _basePort, value, () => BasePort, () =>
                {
                    _serviceConfiguration.BasePort = BasePort;
                    _serviceConfiguration.Save();
                });
            }
        }
        #endregion


        #region Private Methods

        /// <summary>
        /// Loads all settings from the database into the viewmodel.
        /// </summary>
        private void LoadSettings()
        {
            // Initialize settings
            _server = _serviceConfiguration.Server;
            _basePort = _serviceConfiguration.BasePort;

            // Raise PropertyChanged events
            RaisePropertyChanged(() => Server);
            RaisePropertyChanged(() => BasePort);
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(LoadSettings);
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<ServiceConfigViewModel> builder)
        {
            builder.DataFormLayout()
                .ContainsProperty(p => p.Server)
                .ContainsProperty(p => p.BasePort);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<ServiceConfigViewModel> builder)
        {
        }

        #endregion
    }
}
