using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Purchasing
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            DataModelHelper.PopulateTableFromXml(@"Data\PurchaseReceiptStatus.xml", dataLayer, s => new PurchaseReceiptStatus(s));
            DataModelHelper.PopulateTableFromXml(@"Data\PurchaseOrderStatus.xml", dataLayer, s => new PurchaseOrderStatus(s));
        }
    }
}
