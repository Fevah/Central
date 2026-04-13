using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Shared.DataModel.Repository
{
    [FacadeType(typeof(IRepositoryFacade))]
    [DisplayField("UserName")]
    public partial class RecordMetadata
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<RecordMetadata> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.UserName)
                .ContainsProperty(p => p.CreatedOn)
                .ContainsProperty(p => p.CreatedBy);

            builder.Property(p => p.Files).Hidden();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<RecordMetadata> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.UserName);
        }

        #endregion
    }
}