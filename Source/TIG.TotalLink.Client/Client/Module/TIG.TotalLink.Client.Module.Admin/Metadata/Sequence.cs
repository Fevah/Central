using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    [FacadeType(typeof(IAdminFacade))]
    [DisplayField("Name")]
    public partial class Sequence
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Sequence> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Code)
                .ContainsProperty(p => p.NextNumber);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.Code)
                    .ContainsProperty(p => p.NextNumber);

            builder.Property(p => p.Name).ReadOnly();
            builder.Property(p => p.Code).Required();
            builder.Property(p => p.NextNumber).Required();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Sequence> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Code);

            builder.Property(p => p.Code).GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.NextNumber).GetEditor<SpinEditorDefinition>().MinValue = 0;
        }

        #endregion
    }
}
