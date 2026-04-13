using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Xml;
using TIG.TotalLink.Server.Core.ServiceMessageInspector;

namespace TIG.TotalLink.Server.Core.ServiceBehavior
{
    public class AuthenticationEndpointBehavior : IEndpointBehavior, IServiceBehavior
    {
        public void Validate(ServiceEndpoint endpoint)
        {
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            var inspector = new AuthenticationServiceMessageInspector();
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(inspector);
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints,
            BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            var wsdlExporter = new WsdlExporter();
            wsdlExporter.ExportEndpoints(serviceDescription.Endpoints,
                new XmlQualifiedName(serviceDescription.Name, serviceDescription.Namespace));

            foreach (var endpointDispatcher in serviceHostBase.ChannelDispatchers.Cast<ChannelDispatcher>().SelectMany(cDispatcher => cDispatcher.Endpoints))
                endpointDispatcher.DispatchRuntime.MessageInspectors.Add(
                    new AuthenticationServiceMessageInspector());
        }
    }
}