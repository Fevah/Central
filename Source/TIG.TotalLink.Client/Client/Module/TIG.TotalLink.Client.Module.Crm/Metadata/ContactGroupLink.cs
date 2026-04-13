using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("ContactGroup")]
    public partial class ContactGroupLink
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<ContactGroupLink> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.ContactGroup)
                .ContainsProperty(p => p.IsPrimary);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.ContactGroup)
                    .ContainsProperty(p => p.IsPrimary);

            builder.Property(p => p.Contact).Hidden();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<ContactGroupLink> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.ContactGroup);
        }

        #endregion
    }
}
