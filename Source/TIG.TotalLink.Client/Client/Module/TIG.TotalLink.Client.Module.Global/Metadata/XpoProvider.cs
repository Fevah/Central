using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Shared.DataModel.Global
{
    [FacadeType(typeof(IGlobalFacade))]
    [DisplayField("Name")]
    public partial class XpoProvider
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<XpoProvider> builder)
        {
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<XpoProvider> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);

            builder.LookUpEditColumnEditors()
                .Property(p => p.FileFilter).Hidden().EndProperty()
                .Property(p => p.HasIntegratedSecurity).Hidden().EndProperty()
                .Property(p => p.HasMultipleDatabases).Hidden().EndProperty()
                .Property(p => p.HasPassword).Hidden().EndProperty()
                .Property(p => p.HasUserName).Hidden().EndProperty()
                .Property(p => p.IsFileBased).Hidden().EndProperty()
                .Property(p => p.IsServerBased).Hidden().EndProperty()
                .Property(p => p.MeanSchemaGeneration).Hidden().EndProperty()
                .Property(p => p.SupportStoredProcedures).Hidden().EndProperty();
        }

        #endregion
    }
}
