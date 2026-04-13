using System.ServiceModel;

namespace TIG.TotalLink.Shared.Contract.Core
{
    [ServiceContract]
    public interface IMethodServiceBase
    {
        /// <summary>
        /// Dummy method for testing connectivity to a service.
        /// </summary>
        [OperationContract]
        void Ping();
    }
}
