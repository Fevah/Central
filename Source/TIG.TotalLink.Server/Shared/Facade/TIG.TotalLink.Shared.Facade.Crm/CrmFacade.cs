using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Crm
{
    [Facade(1, "Main")]
    public class CrmFacade : FacadeBase<Contact, IMethodServiceBase>, ICrmFacade
    {
        #region Constructors

        public CrmFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion
    }
}
