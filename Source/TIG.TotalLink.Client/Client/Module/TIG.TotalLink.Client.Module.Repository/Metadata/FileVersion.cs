using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Shared.DataModel.Repository
{
    [DisplayField("Version")]
    [FacadeType(typeof(IRepositoryFacade))]
    public partial class FileVersion
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<FileVersion> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Server)
                .ContainsProperty(p => p.Version)
                .ContainsProperty(p => p.File)
                .ContainsProperty(p => p.DataStore);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Server)
                    .ContainsProperty(p => p.Version)
                    .ContainsProperty(p => p.File)
                    .ContainsProperty(p => p.DataStore);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<FileVersion> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Version);
        }

        #endregion
    }
}