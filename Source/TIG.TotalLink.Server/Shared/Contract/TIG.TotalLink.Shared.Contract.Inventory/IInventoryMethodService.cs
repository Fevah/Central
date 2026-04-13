using System.ServiceModel;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.Contract.Inventory
{
    [ServiceContract]
    public interface IInventoryMethodService : IMethodServiceBase
    {
        #region Public Methods

        /// <summary>
        /// Adds a new StockAdjustment, and updates related PhysicalStock entries.
        /// </summary>
        /// <param name="stockAdjustmentJson">Details for the new StockAdjustment, serialized as a Json string.</param>
        /// <returns>An array of changes that occurred.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        EntityChange[] AddStockAdjustment(string stockAdjustmentJson);

        #endregion
    }
}
