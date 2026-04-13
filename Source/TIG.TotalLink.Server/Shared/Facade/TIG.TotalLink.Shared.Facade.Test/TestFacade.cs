using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Test;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Test
{
    [Facade(1, "Main")]
    public class TestFacade : FacadeBase<TestObject, IMethodServiceBase>, ITestFacade
    {
        #region Constructors

        public TestFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion
    }
}
