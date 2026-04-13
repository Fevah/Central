using System.Linq;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Provider;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    [FacadeType(typeof(IAdminFacade))]
    [DisplayField("Name")]
    public partial class Panel
    {
        #region Private Fields

        private static IWidgetProvider _widgetProvider;
        private string _documentName;
        private string _panelGroupName;
        private WidgetViewModel _widget;
        private bool _widgetChanging;

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the parent Document.
        /// We can't assign the Document to a Panel until after the Panel is saved, but we want to display the parent Document so the user knows the panel is being added to the right place.
        /// So we display this property on the add dialog, instead of the main Document property.
        /// </summary>
        [NonPersistent]
        [DoNotCopy]
        public string DocumentName
        {
            get { return _documentName; }
            set { SetProperty(ref _documentName, value, () => DocumentName); }
        }

        /// <summary>
        /// The name of the parent PanelGroup.
        /// We can't assign the PanelGroup to a Panel until after the Panel is saved, but we want to display the parent PanelGroup so the user knows the panel is being added to the right place.
        /// So we display this property on the add dialog, instead of the main PanelGroup property.
        /// </summary>
        [NonPersistent]
        [DoNotCopy]
        public string PanelGroupName
        {
            get { return _panelGroupName; }
            set { SetProperty(ref _panelGroupName, value, () => PanelGroupName); }
        }

        /// <summary>
        /// The widget that is displayed in this panel.
        /// </summary>
        [NonPersistent]
        [DoNotCopy]
        public WidgetViewModel Widget
        {
            get { return _widget; }
            set
            {
                var oldWidget = _widget;

                SetProperty(ref _widget, value, () => Widget, () =>
                {
                    // Abort if the Widget was changed because the ViewName changed
                    if (_widgetChanging)
                        return;

                    // Set the Name and ViewName from the Widget
                    _widgetChanging = true;

                    if (oldWidget == null || oldWidget.Name == Name)
                        Name = (_widget != null ? _widget.Name : null);

                    ViewName = (_widget != null ? _widget.ViewName : null);
                    _widgetChanging = false;
                });
            }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// An instance of the WidgetProvider.
        /// </summary>
        private static IWidgetProvider WidgetProvider
        {
            get
            {
                if (_widgetProvider == null)
                    _widgetProvider = AutofacViewLocator.Default.Resolve<IWidgetProvider>();

                return _widgetProvider;
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Assigns the value of the Widget property by looking up the widget specified in ViewName.
        /// </summary>
        private void SetWidgetFromViewName()
        {
            // Abort if the ViewName was changed because the Widget changed
            if (_widgetChanging)
                return;

            // Set the Widget from the ViewName
            _widgetChanging = true;
            Widget = WidgetProvider.Widgets.FirstOrDefault(w => w.ViewName == ViewName);
            _widgetChanging = false;
        }

        #endregion


        #region Overrides

        protected override void OnLoaded()
        {
            base.OnLoaded();

            SetWidgetFromViewName();
        }

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);

            switch (propertyName)
            {
                case "ViewName":
                    SetWidgetFromViewName();
                    break;
            }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Panel> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Document)
                .ContainsProperty(p => p.PanelGroup)
                .ContainsProperty(p => p.Widget);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.DocumentName)
                    .ContainsProperty(p => p.Document)
                    .ContainsProperty(p => p.PanelGroup)
                    .ContainsProperty(p => p.PanelGroupName)
                    .ContainsProperty(p => p.Widget);

            builder.Property(p => p.Name).Required();
            builder.Property(p => p.Document).ReadOnly();
            builder.Property(p => p.PanelGroup).DisplayName("Group");
            builder.Property(p => p.Widget)
                .Required();
            builder.Property(p => p.DocumentName)
                .DisplayName("Document")
                .ReadOnly();
            builder.Property(p => p.PanelGroupName)
                .DisplayName("Group")
                .ReadOnly();

            builder.Property(p => p.ViewName).NotAutoGenerated();
            builder.Property(p => p.PanelDatas).NotAutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Panel> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Document)
                .ContainsProperty(p => p.PanelGroup)
                .ContainsProperty(p => p.Name);

            builder.GridBaseColumnEditors()
                .Property(p => p.DocumentName).Hidden().EndProperty()
                .Property(p => p.PanelGroupName).Hidden().EndProperty();

            builder.Property(p => p.PanelGroup)
                .AllowNull()
                .GetEditor<LookUpEditorDefinition>().FilterMethod = context => CriteriaOperator.Parse("Document.Oid = ?", ((Panel)context).Document.Oid);

            var widgetProvider = AutofacViewLocator.Default.Resolve<IWidgetProvider>();
            builder.Property(p => p.Widget).ReplaceEditor(new LookUpEditorDefinition()
            {
                ItemsSource = (widgetProvider != null ? widgetProvider.Widgets.Where(w => !string.IsNullOrWhiteSpace(w.ViewName)) : null),
                EntityType = typeof(WidgetViewModel)
            });
        }

        /// <summary>
        /// Builds metadata for form editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        /// <param name="dataLayoutControl">The DataLayoutControlEx that is displaying the object.</param>
        public void BuildFormMetadata(EditorMetadataBuilder<Panel> builder, DataLayoutControlEx dataLayoutControl)
        {
            if (dataLayoutControl.EditMode == DetailEditMode.Add)
            {
                builder.DataFormEditors()
                    .Property(p => p.Document).Hidden().EndProperty()
                    .Property(p => p.PanelGroup).Hidden().EndProperty()
                    .Property(p => p.Widget).NotReadOnly().EndProperty();
            }
            else
            {
                builder.DataFormEditors()
                    .Property(p => p.DocumentName).Hidden().EndProperty()
                    .Property(p => p.PanelGroupName).Hidden().EndProperty()
                    .Property(p => p.Widget).ReadOnly().EndProperty();
            }
        }

        #endregion
    }
}
