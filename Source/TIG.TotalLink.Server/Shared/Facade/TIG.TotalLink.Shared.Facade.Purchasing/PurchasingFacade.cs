using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Purchasing;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Purchasing
{
    [Facade(1, "Main")]
    public class PurchasingFacade : FacadeBase<PurchaseOrder, IMethodServiceBase>, IPurchasingFacade
    {
        #region Constructors

        public PurchasingFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion
    }
}
