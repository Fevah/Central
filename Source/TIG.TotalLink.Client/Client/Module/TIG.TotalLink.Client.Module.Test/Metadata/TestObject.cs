using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Utils.Design;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Editor.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Test;
using TIG.TotalLink.Shared.Facade.Test;

namespace TIG.TotalLink.Shared.DataModel.Test
{
    [FacadeType(typeof(ITestFacade))]
    [DisplayField("Text")]
    //[DialogSize(500, 600)]
    public partial class TestObject
    {
        #region Private Fields

        private ICommand _buttonCommand;
        private ObservableCollection<string> _listBox;
        private ObservableCollection<string> _checkedListBox;
        private ObservableCollection<string> _uploadList;
        private byte[] _image;

        #endregion


        #region Public Properties

        /// <summary>
        /// Example command that will be displayed as a Button editor.
        /// </summary>
        [NonPersistent]
        [DoNotCopy]
        public ICommand ButtonCommand
        {
            get { return _buttonCommand ?? (_buttonCommand = new DelegateCommand(OnButtonExecute, OnButtonCanExecute)); }
        }

        /// <summary>
        /// Example collection that will be displayed as a ListBox editor.
        /// </summary>
        [NonPersistent]
        public ObservableCollection<string> ListBox
        {
            get
            {
                return _listBox ?? (
                    _listBox = new ObservableCollection<string>()
                    {
                        "Item 1",
                        "Item 2",
                        "Item 3",
                        "Item 4",
                        "Item 5"
                    });
            }
        }

        /// <summary>
        /// Example collection that will be displayed as a CheckedListBox editor.
        /// At runtime, this property will contain items selected from the ListBox property.
        /// </summary>
        [NonPersistent]
        public ObservableCollection<string> CheckedListBox
        {
            get
            {
                return _checkedListBox ?? (_checkedListBox = new ObservableCollection<string>());
            }
        }

        [NonPersistent]
        public ObservableCollection<string> UploadList
        {
            get { return _uploadList ?? (_uploadList = new ObservableCollection<string>()); }
        }

        [NonPersistent]
        public byte[] Image
        {
            get { return _image ?? (_image = DXImageHelper.GetImageSource("Add", ImageSize.Size16x16).GetBytes()); }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the ButtonCommand.
        /// </summary>
        private void OnButtonExecute()
        {
            DXMessageBox.Show(string.Format("Button was pressed on entity '{0}'.", this));
        }

        /// <summary>
        /// CanExecute method for the ButtonCommand.
        /// </summary>
        private bool OnButtonCanExecute()
        {
            return Checkbox;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<TestObject> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Oid)
                .ContainsProperty(p => p.Text)
                .ContainsProperty(p => p.SpinInt)
                .ContainsProperty(p => p.SpinLong)
                .ContainsProperty(p => p.Checkbox)
                .ContainsProperty(p => p.ButtonCommand)
                .ContainsProperty(p => p.Label)
                .ContainsProperty(p => p.DateTime)
                .ContainsProperty(p => p.HyperLink)
                .ContainsProperty(p => p.Progress)
                .ContainsProperty(p => p.Password)
                .ContainsProperty(p => p.IncrementingTime)
                .ContainsProperty(p => p.LookUp)
                .ContainsProperty(p => p.AltLookUp)
                .ContainsProperty(p => p.TestObjectGrids)
                .ContainsProperty(p => p.RichText)
                .ContainsProperty(p => p.Comments)
                .ContainsProperty(p => p.Memo)
                .ContainsProperty(p => p.Currency)
                .ContainsProperty(p => p.Decimal)
                .ContainsProperty(p => p.Combo)
                .ContainsProperty(p => p.Option)
                .ContainsProperty(p => p.Image);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Oid)
                    .ContainsProperty(p => p.Text)
                    .ContainsProperty(p => p.SpinInt)
                    .ContainsProperty(p => p.SpinLong)
                    .ContainsProperty(p => p.Checkbox)
                    .ContainsProperty(p => p.Label)
                    .ContainsProperty(p => p.DateTime)
                    .ContainsProperty(p => p.HyperLink)
                    .ContainsProperty(p => p.Progress)
                    .ContainsProperty(p => p.Password)
                    .ContainsProperty(p => p.IncrementingTime)
                    .ContainsProperty(p => p.LookUp)
                    .ContainsProperty(p => p.AltLookUp)
                    .ContainsProperty(p => p.Memo)
                    .ContainsProperty(p => p.Currency)
                    .ContainsProperty(p => p.Decimal)
                    .ContainsProperty(p => p.Combo)
                    .ContainsProperty(p => p.Option)
                    .ContainsProperty(p => p.ButtonCommand)
                    .ContainsProperty(p => p.Image)
                .EndGroup()
                .Group("TestObjectGrids")
                    .ContainsProperty(p => p.TestObjectGrids)
                .EndGroup()
                .Group("RichText")
                    .ContainsProperty(p => p.RichText)
                .EndGroup()
                .Group("Comments")
                    .ContainsProperty(p => p.Comments)
                .EndGroup()
                .Group("ListBox")
                    .ContainsProperty(p => p.ListBox)
                .EndGroup()
                .Group("CheckedListBox")
                    .ContainsProperty(p => p.CheckedListBox)
                .EndGroup()
                .Group("Upload Select")
                    .ContainsProperty(p => p.UploadList);

            builder.Property(p => p.ButtonCommand).DisplayName("Button");
            builder.Property(p => p.TestObjectGrids).AutoGenerated();
            builder.Property(p => p.ListBox).AutoGenerated();
            builder.Property(p => p.CheckedListBox).AutoGenerated();
            builder.Property(p => p.UploadList).AutoGenerated();
            builder.Property(p => p.Image).AutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<TestObject> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Text);

            builder.Property(p => p.Oid).Visible();

            builder.Property(p => p.SpinLong).AllowNull();

            builder.Property(p => p.Label)
                .ReplaceEditor(new LabelEditorDefinition { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis })
                .HideLabel();

            builder.Property(p => p.DateTime)
                .GetEditor<DateTimeEditorDefinition>().ShowTime = true;

            builder.Property(p => p.Password)
                .ReplaceEditor(new PasswordEditorDefinition());

            builder.Property(p => p.Progress)
                .ReplaceEditor(new ProgressEditorDefinition { Minimum = 0, Maximum = 1000 });

            builder.Property(p => p.HyperLink)
                .ReplaceEditor(new HyperLinkEditorDefinition());

            builder.Property(p => p.IncrementingTime)
                .ReplaceEditor(new IncrementingTimeEditorDefinition());

            builder.Property(p => p.RichText)
                .ReplaceEditor(new RichTextEditorDefinition())
                .UnlimitedLength()
                .HideLabel();

            builder.Property(p => p.Comments)
                .ReplaceEditor(new CommentEditorDefinition())
                .HideLabel();

            builder.Property(p => p.Memo)
                .ReplaceEditor(new MemoEditorDefinition())
                .UnlimitedLength();

            builder.Property(p => p.Currency)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.Decimal)
                .GetEditor<DecimalEditorDefinition>().Decimals = 3;

            builder.Property(p => p.Combo)
                .ReplaceEditor(new ComboEditorDefinition(typeof(TestEnum))
                {
                    DisplayMode = EnumEditorDefinitionBase.DisplayModes.ImageAndText
                });

            builder.DataFormEditors().Property(p => p.Option)
                .ReplaceEditor(new OptionEditorDefinition(typeof(TestEnum)));

            builder.GridBaseColumnEditors().Property(p => p.Option)
                .ReplaceEditor(new PopupOptionEditorDefinition(typeof(TestEnum)));

            builder.Property(p => p.ButtonCommand)
                .HideLabel();

            builder.Property(p => p.ListBox)
                .HideLabel();

            builder.Property(p => p.LookUp)
                .AllowNull();

            builder.Property(p => p.AltLookUp)
                .AllowNull()
                .GetEditor<LookUpEditorDefinition>().DisplayMember = "AltName";

            var gridsEditor = new GridEditorDefinition()
            {
                EntityType = typeof(TestObjectGrid),
                FilterMethod = context => CriteriaOperator.Parse("TestObject.Oid = ?", ((TestObject)context).Oid)
            };
            builder.DataFormEditors().Property(p => p.TestObjectGrids)
                .ReplaceEditor(gridsEditor)
                .HideLabel();

            var gridsPopupEditor = new PopupGridEditorDefinition()
            {
                EntityType = typeof(TestObjectGrid),
                FilterMethod = context => CriteriaOperator.Parse("TestObject.Oid = ?", ((TestObject)context).Oid)
            };
            builder.GridBaseColumnEditors().Property(p => p.TestObjectGrids)
                .ReplaceEditor(gridsPopupEditor);

            var checkedListBoxEditor = new CheckedListBoxEditorDefinition()
            {
                ItemsSourcePropertyName = "ListBox"
            };
            builder.Property(p => p.CheckedListBox)
                .ReplaceEditor(checkedListBoxEditor)
                .HideLabel();

            builder.Property(p => p.UploadList)
                .ReplaceEditor(new UploadEditorDefinition
                {
                    FileFilter = "All Images | *.bmp;*.jpg;*.jpeg;*.gif;*.png;*.tif | All Documents | *.doc;*.docx;*xls;*xlsx"
                })
                .HideLabel();

            builder.Property(p => p.Image)
                .ReplaceEditor(new ImageEditorDefinition());
        }

        #endregion
    }
}
