using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using TIG.TotalLink.Shared.Facade.Core.ServiceClientMessageInspector;

namespace TIG.TotalLink.Shared.Facade.Core.ServiceClientBehavior
{
    public class AuthenticationEndpointClientBehavior : IEndpointBehavior
    {
        private readonly AuthenticationClientMessageInspector _inspector;

        public AuthenticationEndpointClientBehavior() { }

        public AuthenticationEndpointClientBehavior(AuthenticationClientMessageInspector inspector)
        {
            _inspector = inspector;
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(_inspector); ;
        }
    }
}