using System;
using System.Linq;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    [FacadeType(typeof(IAdminFacade))]
    [DisplayField("Name")]
    public partial class RibbonItem
    {
        #region Private Fields

        private Document _showDocumentParameter;

        #endregion


        #region Public Properties

        /// <summary>
        /// Specifies the document to show when the CommandType = ShowDocument.
        /// </summary>
        [NonPersistent]
        public Document ShowDocumentParameter
        {
            get { return _showDocumentParameter; }
            set
            {
                SetProperty(ref _showDocumentParameter, value, () => ShowDocumentParameter, () =>
                {
                    var newCommandParameter = (_showDocumentParameter != null ? _showDocumentParameter.Oid.ToString() : null);
                    if (CommandParameter != newCommandParameter)
                        CommandParameter = newCommandParameter;
                });
            }
        }

        #endregion


        #region Overrides

        protected override void OnLoaded()
        {
            base.OnLoaded();

            // If the CommandType is ShowDocument, then set the ShowDocumentParameter based on the CommandParameter
            if (CommandType == CommandType.ShowDocument && Session.IsConnected)
            {
                Guid documentOid;
                if (Guid.TryParse(CommandParameter, out documentOid))
                    ShowDocumentParameter = Session.Query<Document>().FirstOrDefault(d => d.Oid == documentOid);
            }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<RibbonItem> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Description)
                .ContainsProperty(p => p.ItemType)
                .ContainsProperty(p => p.RibbonGroup)
                .ContainsProperty(p => p.CommandType)
                .ContainsProperty(p => p.CommandParameter);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.Description)
                    .ContainsProperty(p => p.ItemType)
                    .ContainsProperty(p => p.RibbonGroup)
                .EndGroup()
                .Group("Command")
                    .ContainsProperty(p => p.CommandType)
                    .ContainsProperty(p => p.CommandParameter)
                    .ContainsProperty(p => p.ShowDocumentParameter);

            builder.Property(p => p.Name).Required();
            builder.Property(p => p.RibbonGroup)
                .Required()
                .DisplayName("Group");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<RibbonItem> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);

            builder.Property(p => p.Description).ReplaceEditor(new MemoEditorDefinition());
            builder.Property(p => p.ItemType).ReplaceEditor(new ComboEditorDefinition(typeof(RibbonItemType)));
            builder.Property(p => p.CommandType).ReplaceEditor(new ComboEditorDefinition(typeof(CommandType)));

            builder.GridBaseColumnEditors()
                .Property(p => p.ShowDocumentParameter).Hidden().EndProperty();
        }

        #endregion
    }
}
