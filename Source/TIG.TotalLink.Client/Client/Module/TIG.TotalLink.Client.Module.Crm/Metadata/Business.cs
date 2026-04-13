using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("FirstName")]
    public partial class Business
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Business> builder)
        {
            builder.DataFormLayout()
                .ContainsProperty(p => p.ContactType)
                .TabbedGroup("Tabs").Group("Additional")
                    .ContainsProperty(p => p.BusinessType)
                    .ContainsProperty(p => p.OwnershipType)
                    .ContainsProperty(p => p.IndustryClass)
                    .ContainsProperty(p => p.TotalStores)
                    .ContainsProperty(p => p.TotalEmployees)
                    .ContainsProperty(p => p.GlobalRegion)
                    .ContainsProperty(p => p.LegacySource)
                .EndGroup();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Business> builder)
        {
            builder.GridBaseColumnEditors()
                .Property(p => p.TotalStores).Hidden().EndProperty()
                .Property(p => p.TotalEmployees).Hidden().EndProperty()
                .Property(p => p.GlobalRegion).Hidden().EndProperty();

            builder.Property(p => p.TotalStores).GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.TotalEmployees).GetEditor<SpinEditorDefinition>().MinValue = 0;
        }

        #endregion
    }
}
