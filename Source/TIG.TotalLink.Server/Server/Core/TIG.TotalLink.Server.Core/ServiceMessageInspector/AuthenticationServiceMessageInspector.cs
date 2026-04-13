using System;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Web.Security;
using DevExpress.Xpo;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Server.Core.ServiceResponse;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Admin;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Server.Core.ServiceMessageInspector
{
    public class AuthenticationServiceMessageInspector : IDispatchMessageInspector
    {
        #region Private Fields

        private static IAdminFacade _adminFacade;

        #endregion


        #region Public Methods

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            // Get the name of the function being executed
            object correlationState = null;
            var action = OperationContext.Current.IncomingMessageHeaders.Action;

            var lastSlashIndex = action.LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
            if (lastSlashIndex == -1)
                return null;

            // Allow the request on some basic functions
            var operationName = action.Substring(lastSlashIndex + 1);
            if (operationName.Equals("Ping")
                || operationName.Equals("GetAutoCreateOption")
                || operationName.Equals("ProcessCookie"))
            {
                return null;
            }

            // Attempt to get the httpRequest property
            object requestProperty;
            if (!request.Properties.TryGetValue("httpRequest", out requestProperty))
                return null;

            // Attempt to convert the property to a HttpRequestMessageProperty
            var requestMessage = requestProperty as HttpRequestMessageProperty;
            if (requestMessage == null)
                return null;

            // Get the parent service instance as a DataServiceBase
            var dataServiceInstance = instanceContext.GetServiceInstance() as DataServiceBase;

            // Get authentication token
            var token = requestMessage.Headers["AuthenticationToken"];
            if (string.IsNullOrEmpty(token) || !Authenticate(token, dataServiceInstance))
            {
                correlationState = new WcfErrorResponseData(HttpStatusCode.Forbidden);
            }

            return correlationState;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Authenticate from token.
        /// </summary>
        /// <param name="token">The token to authenticate with.</param>
        /// <param name="dataServiceInstance">The DataServiceBase that the method is being authenticated on, or null if the parent service is not a DataServiceBase.</param>
        /// <returns>True if the token could be authenticated; otherwise false.</returns>
        private static bool Authenticate(string token, DataServiceBase dataServiceInstance)
        {
            // Fail if the token is empty
            if (string.IsNullOrWhiteSpace(token))
                return false;

            // Attempt to extract the user data from the token
            UserData userData;
            try
            {
                var ticket = FormsAuthentication.Decrypt(token);
                if (ticket == null)
                    return false;

                userData = new UserData(ticket.UserData);
            }
            catch (Exception)
            {
                return false;
            }
            
            // Success if the guid is a service user oid
            if (DefaultUserCache.IsServiceUser(userData.Oid))
                return true;

            // If the method being authenticated belongs to the MainDataService, then use the local data layer to look up the user
            if (dataServiceInstance != null && dataServiceInstance.GetType().Name.Contains("MainDataService"))
            {
                var mainDataLayer = dataServiceInstance.GetLocalDataLayer<User>();
                using (var uow = new UnitOfWork(mainDataLayer))
                {
                    if (uow.Query<User>().Any(u => u.Oid != userData.Oid))
                        return true;

                    return false;
                }
            }

            // If the method being authenticated does not belong to the MainDataService, then use the admin facade to look up the user
            var adminFacade = GetAdminDataFacade();
            if (adminFacade.ExecuteQuery(uow => uow.Query<User>().Where(p => p.Oid != userData.Oid)).Any())
                return true;

            // Fail if we didn't find the user anywhere
            return false;
        }

        /// <summary>
        /// Initializes and returns a AdminFacade connected to the data service only.
        /// </summary>
        /// <returns>An AdminFacade.</returns>
        private static IAdminFacade GetAdminDataFacade()
        {
            if (_adminFacade == null)
                _adminFacade = new AdminFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_adminFacade != null && !_adminFacade.IsDataConnected)
                    _adminFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Admin Facade!", ex));
            }

            return _adminFacade;
        }

        #endregion
    }
}