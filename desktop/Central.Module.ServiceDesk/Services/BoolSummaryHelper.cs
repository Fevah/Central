using DevExpress.Data;
using DevExpress.Xpf.Grid;

namespace Central.Module.ServiceDesk.Services;

public static class BoolSummaryHelper
{
    public static void Wire(GridControl grid, string boolFieldName)
    {
        grid.CustomSummary += (sender, e) =>
        {
            if (e.Item is not GridSummaryItem si || si.FieldName != boolFieldName || !e.IsTotalSummary) return;
            if (si.SummaryType != SummaryItemType.Custom) return;

            switch (e.SummaryProcess)
            {
                case CustomSummaryProcess.Start:
                    e.TotalValue = 0;
                    break;
                case CustomSummaryProcess.Calculate:
                    if (e.FieldValue is true)
                        e.TotalValue = (int)e.TotalValue + 1;
                    break;
            }
        };
    }
}
