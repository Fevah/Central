using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    [FacadeType(typeof(IAdminFacade))]
    [DisplayField("Subject")]
    public partial class Appointment
    {

    }
}