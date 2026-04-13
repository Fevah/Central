using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Workflow;

namespace TIG.TotalLink.Shared.DataModel.Workflow
{
    [FacadeType(typeof(IWorkflowFacade))]
    [DisplayField("Name")]
    public partial class Workflow
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Workflow> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.WorkflowActivity)
                .ContainsProperty(p => p.PublishedDate)
                .ContainsProperty(p => p.UnpublishedDate);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.WorkflowActivity)
                    .ContainsProperty(p => p.PublishedDate)
                    .ContainsProperty(p => p.UnpublishedDate);

            builder.Property(p => p.PublishedDate).ReadOnly();
            builder.Property(p => p.UnpublishedDate).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Workflow> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);

            builder.Property(p => p.PublishedDate).GetEditor<DateTimeEditorDefinition>().ShowTime = true;
            builder.Property(p => p.UnpublishedDate).GetEditor<DateTimeEditorDefinition>().ShowTime = true;
        }

        #endregion
    }
}
