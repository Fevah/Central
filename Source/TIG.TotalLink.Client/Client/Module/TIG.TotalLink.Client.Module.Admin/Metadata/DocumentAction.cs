using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    [FacadeType(typeof(IAdminFacade))]
    [DisplayField("Name")]
    public partial class DocumentAction
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DocumentAction> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Document);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.Document);

            builder.Property(p => p.Name).Required();
            builder.Property(p => p.Document).Required();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DocumentAction> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);
        }

        #endregion
    }
}
