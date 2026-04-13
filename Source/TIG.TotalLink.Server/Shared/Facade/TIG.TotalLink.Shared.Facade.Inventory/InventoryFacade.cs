using System.Threading.Tasks;
using TIG.TotalLink.Shared.Contract.Inventory;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;
using TIG.TotalLink.Shared.Facade.Core.Extension;

namespace TIG.TotalLink.Shared.Facade.Inventory
{
    [Facade(1, "Main", 3, "Inventory")]
    public class InventoryFacade : FacadeBase<Sku, IInventoryMethodService>, IInventoryFacade
    {
        #region Constructors

        public InventoryFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a new StockAdjustment, and updates related PhysicalStock entries.
        /// </summary>
        /// <param name="stockAdjustment">The new StockAdjustment.</param>
        /// <returns>An array of changes that occurred.</returns>
        public EntityChange[] AddStockAdjustment(StockAdjustment stockAdjustment)
        {
            return MethodFacade.Execute(m => m.AddStockAdjustment(stockAdjustment.SerializeToJson()));
        }

        /// <summary>
        /// Asynchronously adds a new StockAdjustment, and updates related PhysicalStock entries.
        /// </summary>
        /// <param name="stockAdjustment">The new StockAdjustment.</param>
        /// <returns>An array of changes that occurred.</returns>
        public async Task<EntityChange[]> AddStockAdjustmentAsync(StockAdjustment stockAdjustment)
        {
            return await MethodFacade.ExecuteAsync(m => m.AddStockAdjustment(stockAdjustment.SerializeToJson())).ConfigureAwait(false);
        }

        #endregion
    }
}
