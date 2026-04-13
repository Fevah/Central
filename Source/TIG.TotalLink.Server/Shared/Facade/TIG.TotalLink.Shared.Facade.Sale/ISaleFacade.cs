using System.Threading.Tasks;
using TIG.TotalLink.Shared.Contract.Sale;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Shared.Facade.Sale
{
    public interface ISaleFacade : IFacadeBase
    {
        #region Public Methods

        /// <summary>
        /// Releases items on a Sales Order.
        /// </summary>
        /// <param name="parameters">A ReleaseSalesOrderParameters object which describes the items to release.</param>
        /// <returns>A ReleaseSalesOrderResult object containing results of the operation.</returns>
        ReleaseSalesOrderResult ReleaseSalesOrder(ReleaseSalesOrderParameters parameters);

        /// <summary>
        /// Asynchronously releases items on a Sales Order.
        /// </summary>
        /// <param name="parameters">A ReleaseSalesOrderParameters object which describes the items to release.</param>
        /// <returns>A ReleaseSalesOrderResult object containing results of the operation.</returns>
        Task<ReleaseSalesOrderResult> ReleaseSalesOrderAsync(ReleaseSalesOrderParameters parameters);

        /// <summary>
        /// Releases items on a Delivery.
        /// </summary>
        /// <param name="parameters">A ReleaseDeliveryParameters object which describes the items to release.</param>
        /// <returns>A ReleaseDeliveryResult object containing results of the operation.</returns>
        ReleaseDeliveryResult ReleaseDelivery(ReleaseDeliveryParameters parameters);

        /// <summary>
        /// Asynchronously releases items on a Delivery.
        /// </summary>
        /// <param name="parameters">A ReleaseDeliveryParameters object which describes the items to release.</param>
        /// <returns>A ReleaseDeliveryResult object containing results of the operation.</returns>
        Task<ReleaseDeliveryResult> ReleaseDeliveryAsync(ReleaseDeliveryParameters parameters);

        #endregion
    }
}