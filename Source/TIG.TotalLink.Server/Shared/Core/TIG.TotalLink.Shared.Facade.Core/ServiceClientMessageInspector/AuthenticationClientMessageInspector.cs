using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace TIG.TotalLink.Shared.Facade.Core.ServiceClientMessageInspector
{
    public class AuthenticationClientMessageInspector : IClientMessageInspector
    {
        private readonly string _ticket;

        public AuthenticationClientMessageInspector(string ticket)
        {
            _ticket = ticket;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            // Attempt to get the httpRequest property
            object requestProperty;
            if (!request.Properties.TryGetValue("httpRequest", out requestProperty))
                return null;

            // Attempt to convert the property to a HttpRequestMessageProperty
            var requestMessage = request.Properties["httpRequest"] as HttpRequestMessageProperty;
            if (requestMessage == null)
                return null;

            // Add the Authentication Token to the headers
            requestMessage.Headers.Add("AuthenticationToken", _ticket);

            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }
    }
}