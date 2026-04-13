using System.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            DataModelHelper.PopulateTableFromXml(@"Data\Sequence.xml", dataLayer,
                s => new Sequence(s),
                (s, v) =>
                    s.QueryInTransaction<Sequence>().Any(i => v.ContainsKey("Name") && i.Name == (string)v["Name"])
                );

            DataModelHelper.PopulateTableFromXml(@"Data\Currency.xml", dataLayer, s => new Currency(s));
            DataModelHelper.PopulateTableFromXml(@"Data\Language.xml", dataLayer, s => new Language(s));
            DataModelHelper.PopulateTableFromXml(@"Data\PaymentTerms.xml", dataLayer, s => new PaymentTerms(s));
            DataModelHelper.PopulateTableFromXml(@"Data\PaymentMethod.xml", dataLayer, s => new PaymentMethod(s));
            DataModelHelper.PopulateTableFromXml(@"Data\ReminderTerms.xml", dataLayer, s => new ReminderTerms(s));
            DataModelHelper.PopulateTableFromXml(@"Data\UnitType.xml", dataLayer, s => new UnitType(s));
            DataModelHelper.PopulateTableFromXml(@"Data\StreetType.xml", dataLayer, s => new StreetType(s));
            DataModelHelper.PopulateTableFromXml(@"Data\AddressValidationApi.xml", dataLayer, s => new AddressValidationApi(s));
            DataModelHelper.PopulateTableFromXml(@"Data\AddressType.xml", dataLayer, s => new AddressType(s));
            DataModelHelper.PopulateTableFromXml(@"Data\ShipmentMethod.xml", dataLayer, s => new ShipmentMethod(s));
            DataModelHelper.PopulateTableFromXml(@"Data\BranchType.xml", dataLayer, s => new BranchType(s));
        }
    }
}
