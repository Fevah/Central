using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("AddressType")]
    public partial class Address
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Address> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Contact)
                .ContainsProperty(p => p.AddressType)
                .ContainsProperty(p => p.IsDefault)
                .ContainsProperty(p => p.UnitType)
                .ContainsProperty(p => p.UnitNumber)
                .ContainsProperty(p => p.StreetNumber)
                .ContainsProperty(p => p.StreetName)
                .ContainsProperty(p => p.StreetType)
                .ContainsProperty(p => p.Postcode)
                .ContainsProperty(p => p.LegacyLine1)
                .ContainsProperty(p => p.LegacyLine2)
                .ContainsProperty(p => p.City);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Contact)
                    .ContainsProperty(p => p.AddressType)
                    .ContainsProperty(p => p.IsDefault)
                    .ContainsProperty(p => p.UnitType)
                    .ContainsProperty(p => p.UnitNumber)
                    .ContainsProperty(p => p.StreetNumber)
                    .ContainsProperty(p => p.StreetName)
                    .ContainsProperty(p => p.StreetType)
                    .ContainsProperty(p => p.Postcode)
                    .ContainsProperty(p => p.LegacyLine1)
                    .ContainsProperty(p => p.LegacyLine2)
                .Group("Additional")
                    .ContainsProperty(p => p.Label)
                    .ContainsProperty(p => p.CompanyName)
                    .ContainsProperty(p => p.AttentionTo)
                    .ContainsProperty(p => p.City)
                    .ContainsProperty(p => p.ValidationAPI)
                    .ContainsProperty(p => p.ValidationDate)
                    .ContainsProperty(p => p.Longitude)
                    .ContainsProperty(p => p.Latitude);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Address> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.AddressType);

            builder.GridBaseColumnEditors()
                .Property(p => p.Label).Hidden().EndProperty()
                .Property(p => p.AttentionTo).Hidden().EndProperty()
                .Property(p => p.ValidationAPI).Hidden().EndProperty()
                .Property(p => p.ValidationDate).Hidden().EndProperty()
                .Property(p => p.CompanyName).Hidden().EndProperty()
                .Property(p => p.Longitude).Hidden().EndProperty()
                .Property(p => p.Latitude).Hidden().EndProperty();
        }

        #endregion
    }
}