using System;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Test;

namespace TIG.TotalLink.Client.Module.Test.Uploader
{
    public class TestObjectUploaderDataModel : UploaderDataModelBase
    {
        #region Private Fields

        private string _text;
        private int _spinInt;
        private bool _checkbox;
        private DateTime _dateTime;
        private string _hyperLink;
        private TestObjectLookUp _lookUp;
        private decimal _currency;
        private string _label;
        private int _progress;

        #endregion


        #region Public Properties

        public string Text
        {
            get { return _text; }
            set { SetProperty(ref _text, value, () => Text); }
        }

        public int SpinInt
        {
            get { return _spinInt; }
            set { SetProperty(ref _spinInt, value, () => SpinInt); }
        }

        public bool Checkbox
        {
            get { return _checkbox; }
            set { SetProperty(ref _checkbox, value, () => Checkbox); }
        }

        public DateTime DateTime
        {
            get { return _dateTime; }
            set { SetProperty(ref _dateTime, value, () => DateTime); }
        }

        public string HyperLink
        {
            get { return _hyperLink; }
            set { SetProperty(ref _hyperLink, value, () => HyperLink); }
        }

        public TestObjectLookUp LookUp
        {
            get { return _lookUp; }
            set { SetProperty(ref _lookUp, value, () => LookUp); }
        }

        public decimal Currency
        {
            get { return _currency; }
            set { SetProperty(ref _currency, value, () => Currency); }
        }

        public string Label
        {
            get { return _label; }
            set { SetProperty(ref _label, value, () => Label); }
        }

        public int Progress
        {
            get { return _progress; }
            set { SetProperty(ref _progress, value, () => Progress); }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<TestObjectUploaderDataModel> builder)
        {
            builder.Property(p => p.Text)
                .Required()
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.SpinInt)
                .Required()
                .ReadOnly();
            builder.Property(p => p.Checkbox)
                .Required()
                .ReadOnly();
            builder.Property(p => p.DateTime)
                .Required()
                .ReadOnly();
            builder.Property(p => p.HyperLink)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.LookUp)
                .ReadOnly();
            builder.Property(p => p.Currency)
                .ReadOnly();
            builder.Property(p => p.Label)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.Progress)
                .ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<TestObjectUploaderDataModel> builder)
        {
            builder.Property(p => p.DateTime).GetEditor<DateTimeEditorDefinition>().ShowTime = true;
            builder.Property(p => p.HyperLink).ReplaceEditor(new HyperLinkEditorDefinition());
            builder.Property(p => p.Currency).ReplaceEditor(new CurrencyEditorDefinition());
            builder.Property(p => p.Label).ReplaceEditor(new LabelEditorDefinition());
            builder.Property(p => p.Progress).ReplaceEditor(new ProgressEditorDefinition());
        }

        #endregion
    }
}
