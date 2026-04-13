using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Integration
{
    public class Populate : IPopulateDataStore
    {
        [Flags]
        private enum SyncMode
        {
            ECommerce = 1,
            TotalLink = 2,
            All = ECommerce | TotalLink
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            using (var uow = new UnitOfWork(dataLayer))
            {
                if (new XPQuery<SyncEntityBundle>(uow).Any())
                    return;

                var syncEntityBundle = new SyncEntityBundle(uow)
                {
                    Name = "Sync Common Entities Between ECommerce And TotalLink Bundle",
                    IsActive = true
                };

                MappingEntity(uow, syncEntityBundle, "Sequence", 10, "Common");

                MappingEntity(uow, syncEntityBundle, "PostingGroup", 10, "Common");

                BuildLocationRelationshipSyncMapping(uow);
                BuildContactRelationshipSyncMapping(uow);
                BuildInventoryRelationshipSyncMapping(uow);
                BuildSaleRelationshipSyncMapping(uow);
                uow.CommitChanges();
            }
        }


        #region Private Methods

        private static void BuildSaleRelationshipSyncMapping(Session uow)
        {
            var syncEntityBundle = new SyncEntityBundle(uow)
            {
                Name = "Sync Sale Relationship Between ECommerce And TotalLink Bundle",
                IsActive = true
            };

            const string folder = "Sale";

            MappingEntity(uow, syncEntityBundle, "EnquiryType", 12, folder);
            MappingEntity(uow, syncEntityBundle, "EmployeeRange", 12, folder);
            MappingEntity(uow, syncEntityBundle, "FindMethod", 12, folder);
            MappingEntity(uow, syncEntityBundle, "EnquiryStatus", 12, folder);
            MappingEntity(uow, syncEntityBundle, "Enquiry", 11, folder, syncMode: SyncMode.ECommerce);
            MappingEntity(uow, syncEntityBundle, "EnquiryItem", 10, folder, syncMode: SyncMode.ECommerce);

            MappingEntity(uow, syncEntityBundle, "SalesOrderStatus", 12, folder);
            MappingEntity(uow, syncEntityBundle, "SalesOrder", 11, folder);
            MappingEntity(uow, syncEntityBundle, "SalesOrderItem", 10, folder);
        }

        private static void BuildInventoryRelationshipSyncMapping(Session uow)
        {
            var syncEntityBundle = new SyncEntityBundle(uow)
            {
                Name = "Sync Inventory Relationship Between ECommerce And TotalLink Bundle",
                IsActive = true
            };

            const string folder = "Inventory";

            MappingEntity(uow, syncEntityBundle, "ColourCategory", 11, folder);
            MappingEntity(uow, syncEntityBundle, "Colour", 10, folder);

            MappingEntity(uow, syncEntityBundle, "SizeRange", 11, folder);
            MappingEntity(uow, syncEntityBundle, "Size", 10, folder);

            MappingEntity(uow, syncEntityBundle, "BarcodeType", 11, folder);
            MappingEntity(uow, syncEntityBundle, "Barcode", 10, folder);

            MappingEntity(uow, syncEntityBundle, "ProductCategory", 10, folder);
            MappingEntity(uow, syncEntityBundle, "ProductType", 10, folder);

            MappingEntity(uow, syncEntityBundle, "Season", 10, folder);
            MappingEntity(uow, syncEntityBundle, "Fabric", 10, folder);
            MappingEntity(uow, syncEntityBundle, "Fit", 11, folder);

            MappingEntity(uow, syncEntityBundle, "StyleClass", 10, folder);
            MappingEntity(uow, syncEntityBundle, "StyleCategory", 10, folder);
            MappingEntity(uow, syncEntityBundle, "StyleGender", 10, folder);
            MappingEntity(uow, syncEntityBundle, "StyleDepartment", 10, folder);


            MappingEntity(uow, syncEntityBundle, "UnitOfMeasure", 10, folder);
            MappingEntity(uow, syncEntityBundle, "BusinessDivision", 10, folder);

            MappingEntity(uow, syncEntityBundle, "Style", 8, folder);
            MappingEntity(uow, syncEntityBundle, "Sku", 7, folder);

            MappingEntity(uow, syncEntityBundle, "PriceRange", 6, folder);

            MappingEntity(uow, syncEntityBundle, "WarehouseLocation", 10, folder);
            MappingEntity(uow, syncEntityBundle, "BinLocation", 9, folder);

            MappingEntity(uow, syncEntityBundle, "StockAdjustmentReason", 10, folder);
            MappingEntity(uow, syncEntityBundle, "PhysicalStockType", 10, folder);

            MappingEntity(uow, syncEntityBundle, "StockAdjustment", 6, folder);
        }

        private static void BuildContactRelationshipSyncMapping(Session uow)
        {
            var syncEntityBundle = new SyncEntityBundle(uow)
            {
                Name = "Sync Contact Relationship Between ECommerce And TotalLink Bundle",
                IsActive = true
            };

            const string folder = "Crm";

            // Mapping Address
            MappingEntity(uow, syncEntityBundle, "AddressType", 10, folder);
            MappingEntity(uow, syncEntityBundle, "AddressValidationApi", 10, folder);
            MappingEntity(uow, syncEntityBundle, "UnitType", 10, folder);
            MappingEntity(uow, syncEntityBundle, "StreetType", 10, folder);
            MappingEntity(uow, syncEntityBundle, "Address", 9, folder);

            // Mapping Contact
            MappingEntity(uow, syncEntityBundle, "IndustryClass", 11, folder);
            MappingEntity(uow, syncEntityBundle, "Industry", 10, folder);
            MappingEntity(uow, syncEntityBundle, "Currency", 10, folder);
            MappingEntity(uow, syncEntityBundle, "Language", 10, folder);
            MappingEntity(uow, syncEntityBundle, "Title", 10, folder);
            MappingEntity(uow, syncEntityBundle, "GlobalRegion", 10, folder);
            MappingEntity(uow, syncEntityBundle, "ChatType", 10, folder);
            MappingEntity(uow, syncEntityBundle, "PaymentTerms", 10, folder, false);
            MappingEntity(uow, syncEntityBundle, "ReminderTerms", 10, folder, false);
            MappingEntity(uow, syncEntityBundle, "PaymentMethod", 10, folder);
            MappingEntity(uow, syncEntityBundle, "OwnershipType", 10, folder);
            MappingEntity(uow, syncEntityBundle, "BusinessType", 10, folder);
            MappingEntity(uow, syncEntityBundle, "ShipmentMethod", 10, folder);
            MappingEntity(uow, syncEntityBundle, "BranchType", 10, folder);
            MappingEntity(uow, syncEntityBundle, "StaffRole", 10, folder);

            // Map to Contact
            MappingEntity(uow, syncEntityBundle, "Contact", 8, folder);

            // Mapping Contact Group Link
            MappingEntity(uow, syncEntityBundle, "ContactGroup", 10, folder);
            MappingEntity(uow, syncEntityBundle, "ContactGroupLink", 7, folder);

            // Mapping Contact Link
            MappingEntity(uow, syncEntityBundle, "ContactLinkType", 11, folder);
            MappingEntity(uow, syncEntityBundle, "ContactLink", 10, folder);
        }

        private static void BuildLocationRelationshipSyncMapping(Session uow)
        {
            var syncEntityBundle = new SyncEntityBundle(uow)
            {
                Name = "Sync Location Relationship Between ECommerce And TotalLink Bundle",
                IsActive = true
            };

            const string folder = "Location";

            // Mapping Country
            MappingEntity(uow, syncEntityBundle, "Country", 3, folder);

            // Mapping State
            MappingEntity(uow, syncEntityBundle, "State", 2, folder);

            // Mapping Postcode
            MappingEntity(uow, syncEntityBundle, "Postcode", 1, folder);
        }

        private static void MappingEntity(Session uow, SyncEntityBundle syncEntityBundle, string entityName, int priority, string mappingFolder, bool needPluralTableName = true, SyncMode syncMode = SyncMode.All)
        {
            var syncEntityForECommerce = new SyncEntity(uow)
            {
                EntityName = needPluralTableName ? ToPlural(entityName) : entityName,
                TableName = needPluralTableName ? ToPlural(entityName) : entityName,
                PrimaryKey = "{DBKey: {Name: 'Oid', Type: 'System.Guid'}, ODataKey: {Name: 'Oid', Type: 'System.Guid'}}",
                IsActive = true,
                AgentPluginId = Guid.Parse("{9D4F6047-15FE-4BB7-BA3B-0B65C3C476C8}"),
                ChangeTrackerPluginId = Guid.Parse("{15680198-1B76-459D-87EF-93A6DFFB7E21}"),
                Bundle = syncEntityBundle,
                PriorityInBundle = priority
            };

            var syncEntityForTotalLink = new SyncEntity(uow)
            {
                EntityName = entityName,
                TableName = entityName,
                PrimaryKey = "{DBKey: {Name: 'Oid', Type: 'System.Guid'}, ODataKey: {Name: 'Oid', Type: 'System.Guid'}}",
                IsActive = true,
                AgentPluginId = Guid.Parse("{4050D6DF-DE42-4C90-B535-73EAF4AB9960}"),
                ChangeTrackerPluginId = Guid.Parse("{15680198-1B76-459D-87EF-93A6DFFB7E21}"),
                Bundle = syncEntityBundle,
                PriorityInBundle = priority
            };

            var mappingPath = string.Format(@"FieldMapping\{1}\{0}ForECommerce2TotalLink.xml", entityName,
                mappingFolder);

            if ((syncMode & SyncMode.ECommerce) == SyncMode.ECommerce)
            {
                new SyncEntityMap(uow)
                {
                    Name = string.Format("Sync {0} from ECommerce to TotalLink", entityName),
                    IsActive = true,
                    SourceEntity = syncEntityForECommerce,
                    TargetEntity = syncEntityForTotalLink,
                    SyncKeyInfo =
                        "{Source: {Name: 'Oid', Type: 'System.Guid'}, Target: {Name: 'Oid', Type: 'System.Guid'}}",
                    MapperPluginId = Guid.Parse("{EBC8AECA-8521-4A9E-A4C0-C3711E829BCC}"),
                    FieldMappings = DataModelHelper.ReadResourceContent(mappingPath)
                };
            }

            if ((syncMode & SyncMode.TotalLink) == SyncMode.TotalLink)
            {
                mappingPath = string.Format(@"FieldMapping\{1}\{0}ForTotalLink2ECommerce.xml", entityName, mappingFolder);

                new SyncEntityMap(uow)
                {
                    Name = string.Format("Sync {0} from TotalLink to ECommerce", entityName),
                    IsActive = true,
                    SourceEntity = syncEntityForTotalLink,
                    TargetEntity = syncEntityForECommerce,
                    SyncKeyInfo =
                        "{Source: {Name: 'Oid', Type: 'System.Guid'}, Target: {Name: 'Oid', Type: 'System.Guid'}}",
                    MapperPluginId = Guid.Parse("{EBC8AECA-8521-4A9E-A4C0-C3711E829BCC}"),
                    FieldMappings = DataModelHelper.ReadResourceContent(mappingPath)
                };
            }
        }

        private static string ToPlural(string word)
        {
            var plural1 = new Regex("(?<keep>[^aeiou]+)y$");
            var plural2 = new Regex("(?<keep>[aeiou]+y)$");
            var plural3 = new Regex("(?<keep>[sxzh]+)$");
            var plural4 = new Regex("(?<keep>[^sxzhy]+)$");

            if (plural1.IsMatch(word))
                return plural1.Replace(word, "${keep}ies");
            if (plural2.IsMatch(word))
                return plural2.Replace(word, "${keep}s");
            if (plural3.IsMatch(word))
                return plural3.Replace(word, "${keep}es");
            if (plural4.IsMatch(word))
                return plural4.Replace(word, "${keep}s");

            return string.Format(@"{0}s", word);
        }

        #endregion
    }
}
