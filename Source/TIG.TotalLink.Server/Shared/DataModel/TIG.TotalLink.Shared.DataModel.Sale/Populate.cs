using System.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Sale
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            DataModelHelper.PopulateTableFromXml(@"Data\Sequence.xml", dataLayer,
                s => new Sequence(s),
                (s, v) => s.QueryInTransaction<Sequence>().Any(i => v.ContainsKey("Name") && i.Name == (string)v["Name"])
            );

            DataModelHelper.PopulateTableFromXml(@"Data\EnquiryType.xml", dataLayer, s => new EnquiryType(s));
            DataModelHelper.PopulateTableFromXml(@"Data\EmployeeRange.xml", dataLayer, s => new EmployeeRange(s));
            DataModelHelper.PopulateTableFromXml(@"Data\FindMethod.xml", dataLayer, s => new FindMethod(s));
            DataModelHelper.PopulateTableFromXml(@"Data\EnquiryStatus.xml", dataLayer, s => new EnquiryStatus(s));
            DataModelHelper.PopulateTableFromXml(@"Data\SalesOrderStatus.xml", dataLayer, s => new SalesOrderStatus(s));
            DataModelHelper.PopulateTableFromXml(@"Data\DeliveryStatus.xml", dataLayer, s => new DeliveryStatus(s));
            DataModelHelper.PopulateTableFromXml(@"Data\InvoiceStatus.xml", dataLayer, s => new InvoiceStatus(s));
        }
    }
}
