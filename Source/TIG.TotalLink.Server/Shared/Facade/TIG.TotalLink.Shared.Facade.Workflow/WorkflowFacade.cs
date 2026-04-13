using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Workflow;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Workflow
{
    [Facade(1, "Main")]
    public class WorkflowFacade : FacadeBase<WorkflowActivity, IMethodServiceBase>, IWorkflowFacade
    {
        #region Constructors

        public WorkflowFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion
    }
}
