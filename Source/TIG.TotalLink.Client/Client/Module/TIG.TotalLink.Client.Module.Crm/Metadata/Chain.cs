using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("FirstName")]
    public partial class Chain
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Chain> builder)
        {
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Chain> builder)
        {
            builder.Group();

            builder.GridBaseColumnEditors()
                .Property(p => p.ContactType).Hidden().EndProperty();
        }

        #endregion
    }
}
