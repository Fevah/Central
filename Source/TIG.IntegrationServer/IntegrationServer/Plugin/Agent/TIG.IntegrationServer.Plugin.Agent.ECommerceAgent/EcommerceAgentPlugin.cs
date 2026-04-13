using System;
using System.ServiceModel;
using System.Xml;
using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Agent.ECommerceAgent.Agent;
using TIG.IntegrationServer.Plugin.Agent.ECommerceAgent.Core;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ServiceContext;

namespace TIG.IntegrationServer.Plugin.Agent.ECommerceAgent
{
    public class EcommerceAgentPlugin : EcommerceAgentPluginBase
    {
        #region Private Properties

        private string _token;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public EcommerceAgentPlugin()
        {
            // Initialize authentication service.
            InitializeAuthenticationService();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initialize authentication service
        /// </summary>
        private void InitializeAuthenticationService()
        {
            var authenticatioinSettings = Configuration.SyncServiceSettings.AuthenticationSettings;

            // Create an endpoint and binding for the service
            var endpoint = new EndpointAddress(authenticatioinSettings.ServiceUri);
            var binding = new BasicHttpBinding
            {
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                TransferMode = TransferMode.Streamed,
                OpenTimeout = new TimeSpan(0, 5, 0),
                CloseTimeout = new TimeSpan(0, 5, 0),
                SendTimeout = new TimeSpan(0, 5, 0),
                ReceiveTimeout = new TimeSpan(0, 5, 0),
                ReaderQuotas = new XmlDictionaryReaderQuotas
                {
                    MaxDepth = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxStringContentLength = int.MaxValue
                }
            };

            var client = new AuthenticationServiceContext(binding, endpoint);
            _token = client.Login(authenticatioinSettings.Login, authenticatioinSettings.Password);
        }

        #endregion


        #region Overrides

        /// <summary>
        /// GetAgent for build agent.
        /// </summary>
        /// <returns></returns>
        public override IAgent BuildAgent()
        {
            var entityServiceSettings = Configuration.SyncServiceSettings;
            var context = new ODataServiceContext(entityServiceSettings.ServiceUri, _token, null);

            var agent = new EcommerceAgent(context);
            return agent;
        }

        /// <summary>
        /// ChangeTrackerConfiguration for change tracker service specification
        /// </summary>
        public override ChangeTrackerConfigurationElement ChangeTrackerConfiguration
        {
            get { return Configuration.ChangeTrackerSettings; }
        }

        #endregion
    }
}
