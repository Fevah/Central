using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Task
{
    [Facade(1, "Main")]
    public class TaskFacade : FacadeBase<DataModel.Task.Task, IMethodServiceBase>, ITaskFacade
    {
        #region Constructors

        public TaskFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion
    }
}
