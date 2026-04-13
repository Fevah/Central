using System.ServiceModel;
using TIG.TotalLink.Shared.Contract.Core;

namespace TIG.TotalLink.Shared.Contract.Sale
{
    [ServiceContract]
    public interface ISaleMethodService : IMethodServiceBase
    {
        #region Public Methods

        /// <summary>
        /// Releases items on a Sales Order.
        /// </summary>
        /// <param name="parameters">A ReleaseSalesOrderParameters object which describes the items to release.</param>
        /// <returns>A ReleaseSalesOrderResult object containing results of the operation.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        ReleaseSalesOrderResult ReleaseSalesOrder(ReleaseSalesOrderParameters parameters);

        /// <summary>
        /// Releases items on a Delivery.
        /// </summary>
        /// <param name="parameters">A ReleaseDeliveryParameters object which describes the items to release.</param>
        /// <returns>A ReleaseDeliveryResult object containing results of the operation.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        ReleaseDeliveryResult ReleaseDelivery(ReleaseDeliveryParameters parameters);

        #endregion
    }
}
