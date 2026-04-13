using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Integration;

namespace TIG.TotalLink.Shared.DataModel.Integration
{
    [FacadeType(typeof(IIntegrationFacade))]
    [DisplayField("AddressType")]
    public partial class SyncEntityMap
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<SyncEntityMap> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.IsActive)
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.SourceEntity)
                .ContainsProperty(p => p.SourceFilter)
                .ContainsProperty(p => p.TargetEntity)
                .ContainsProperty(p => p.SyncKeyInfo)
                .ContainsProperty(p => p.MapperPluginId)
                .ContainsProperty(p => p.Order); 
            
            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.IsActive)
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.SourceEntity)
                    .ContainsProperty(p => p.SourceFilter)
                    .ContainsProperty(p => p.TargetEntity)
                    .ContainsProperty(p => p.SyncKeyInfo)
                    .ContainsProperty(p => p.MapperPluginId)
                    .ContainsProperty(p => p.Order)
                .EndGroup()
                .Group("Field Mapping")
                    .ContainsProperty(p => p.FieldMappings);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<SyncEntityMap> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);

            builder.Property(p => p.MapperPluginId)
                .ReplaceEditor(new TextEditorDefinition());

            builder.Property(p => p.FieldMappings)
                .ReplaceEditor(new MemoEditorDefinition());
        }

        #endregion
    }
}