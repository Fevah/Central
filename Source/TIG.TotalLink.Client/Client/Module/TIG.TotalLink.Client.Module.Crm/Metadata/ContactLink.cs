using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("ContactLinkType")]
    public partial class ContactLink
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<ContactLink> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Target)
                .ContainsProperty(p => p.Source)
                .ContainsProperty(p => p.ContactLinkType);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Target)
                    .ContainsProperty(p => p.Source)
                    .ContainsProperty(p => p.ContactLinkType);

            builder.Property(p => p.Source).DisplayName("Child");
            builder.Property(p => p.Target).DisplayName("Parent");
            builder.Property(p => p.ContactLinkType).DisplayName("Link Type");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<ContactLink> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.ContactLinkType);
        }

        #endregion
    }
}
