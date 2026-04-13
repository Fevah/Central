using System.ComponentModel;

namespace TIG.TotalLink.Shared.DataModel.Core.Enum.Admin
{
    public enum UserType
    {
        System,
        [Description("TotalLink")]
        TotalLink,
        ActiveDirectory
    }
}
