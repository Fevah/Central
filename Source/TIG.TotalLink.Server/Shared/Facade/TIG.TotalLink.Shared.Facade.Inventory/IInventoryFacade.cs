using System.Threading.Tasks;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Shared.Facade.Inventory
{
    public interface IInventoryFacade : IFacadeBase
    {
        /// <summary>
        /// Adds a new StockAdjustment, and updates related PhysicalStock entries.
        /// </summary>
        /// <param name="stockAdjustment">The new StockAdjustment.</param>
        /// <returns>An array of changes that occurred.</returns>
        EntityChange[] AddStockAdjustment(StockAdjustment stockAdjustment);

        /// <summary>
        /// Asynchronously adds a new StockAdjustment, and updates related PhysicalStock entries.
        /// </summary>
        /// <param name="stockAdjustment">The new StockAdjustment.</param>
        /// <returns>An array of changes that occurred.</returns>
        Task<EntityChange[]> AddStockAdjustmentAsync(StockAdjustment stockAdjustment);
    }
}