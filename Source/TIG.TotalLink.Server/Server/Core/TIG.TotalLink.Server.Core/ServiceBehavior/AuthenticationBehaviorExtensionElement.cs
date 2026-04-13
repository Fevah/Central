using System;
using System.ServiceModel.Configuration;

namespace TIG.TotalLink.Server.Core.ServiceBehavior
{
    public class AuthenticationBehaviorExtensionElement : BehaviorExtensionElement
    {
        protected override object CreateBehavior()
        {
            return new AuthenticationEndpointBehavior();
        }

        public override Type BehaviorType
        {
            get { return typeof(AuthenticationEndpointBehavior); }
        }
    }
}