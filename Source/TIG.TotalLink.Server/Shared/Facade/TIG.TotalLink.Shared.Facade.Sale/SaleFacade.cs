using System.Reflection;
using System.Threading.Tasks;
using TIG.TotalLink.Shared.Contract.Sale;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.DataModel.Sale;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Sale
{
    [Facade(1, "Main", 5, "Sale")]
    public class SaleFacade : FacadeBase<Enquiry, ISaleMethodService>, ISaleFacade
    {
        #region Constructors

        public SaleFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration, Assembly.GetAssembly(typeof(Company)))
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Releases items on a Sales Order.
        /// </summary>
        /// <param name="parameters">A ReleaseSalesOrderParameters object which describes the items to release.</param>
        /// <returns>A ReleaseSalesOrderResult object containing results of the operation.</returns>
        public ReleaseSalesOrderResult ReleaseSalesOrder(ReleaseSalesOrderParameters parameters)
        {
            return MethodFacade.Execute(m => m.ReleaseSalesOrder(parameters));
        }

        /// <summary>
        /// Asynchronously releases items on a Sales Order.
        /// </summary>
        /// <param name="parameters">A ReleaseSalesOrderParameters object which describes the items to release.</param>
        /// <returns>A ReleaseSalesOrderResult object containing results of the operation.</returns>
        public async Task<ReleaseSalesOrderResult> ReleaseSalesOrderAsync(ReleaseSalesOrderParameters parameters)
        {
            return await MethodFacade.ExecuteAsync(m => m.ReleaseSalesOrder(parameters)).ConfigureAwait(false);
        }

        /// <summary>
        /// Releases items on a Delivery.
        /// </summary>
        /// <param name="parameters">A ReleaseDeliveryParameters object which describes the items to release.</param>
        /// <returns>A ReleaseDeliveryResult object containing results of the operation.</returns>
        public ReleaseDeliveryResult ReleaseDelivery(ReleaseDeliveryParameters parameters)
        {
            return MethodFacade.Execute(m => m.ReleaseDelivery(parameters));
        }

        /// <summary>
        /// Asynchronously releases items on a Delivery.
        /// </summary>
        /// <param name="parameters">A ReleaseDeliveryParameters object which describes the items to release.</param>
        /// <returns>A ReleaseDeliveryResult object containing results of the operation.</returns>
        public async Task<ReleaseDeliveryResult> ReleaseDeliveryAsync(ReleaseDeliveryParameters parameters)
        {
            return await MethodFacade.ExecuteAsync(m => m.ReleaseDelivery(parameters)).ConfigureAwait(false);
        }

        #endregion
    }
}
