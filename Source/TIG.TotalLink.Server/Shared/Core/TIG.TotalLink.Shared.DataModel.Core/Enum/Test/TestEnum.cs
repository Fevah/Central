using System.ComponentModel;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;

namespace TIG.TotalLink.Shared.DataModel.Core.Enum.Test
{
    public enum TestEnum
    {
        [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Test;component/Image/16x16/Flag_Blue.png")]
        [EnumToolTip("Blue")]
        None,

        [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Test;component/Image/16x16/Flag_Green.png")]
        [EnumToolTip("Green")]
        Option1,

        [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Test;component/Image/16x16/Flag_Purple.png")]
        [EnumToolTip("Purple")]
        Option234,

        [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Test;component/Image/16x16/Flag_Red.png")]
        [EnumToolTip("Red")]
        AnOptionWithMultipleWords,

        [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Test;component/Image/16x16/Flag_Yellow.png")]
        [EnumToolTip("Yellow")]
        AnOptionWithTLAInTheMiddle,

        [EnumToolTip("No Image")]
        AnOptionWithNoImage,

        [Description]
        HiddenOption,

        [Description("Alternative Name")]
        RenamedOption
    }
}
