using System;
using System.Windows.Controls;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Client.Module.Global.ViewModel.Widget
{
    public class ReferenceConfigViewModel : LocalDetailViewModelBase
    {
        #region Private Fields

        private int _systemCode;
        private string _referenceValueFormat;
        private string _referenceDisplayFormat;
        private string _referenceDisplayClean;

        private int _testSystemCode;
        private int _testSequenceCode;
        private long _testSequenceNumber;
        private long _testReferenceValue;
        private string _testReferenceDisplay;
        private string _testReferenceDisplayCleaned;

        private readonly IGlobalFacade _globalFacade;

        #endregion


        #region Constructors

        public ReferenceConfigViewModel()
        {
        }

        public ReferenceConfigViewModel(IGlobalFacade globalFacade)
            : this()
        {
            _globalFacade = globalFacade;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The code for this system to use in reference number generation.
        /// </summary>
        public int SystemCode
        {
            get { return _systemCode; }
            set
            {
                SetProperty(ref _systemCode, value, () => SystemCode, async () =>
                {
                    await _globalFacade.SetSettingAsync("SystemCode", _systemCode.ToString());
                });
            }
        }

        /// <summary>
        /// A numeric format string for building reference numbers.
        /// </summary>
        public string ReferenceValueFormat
        {
            get { return _referenceValueFormat; }
            set
            {
                SetProperty(ref _referenceValueFormat, value, () => ReferenceValueFormat, async () =>
                {
                    await _globalFacade.SetSettingAsync("ReferenceValueFormat", _referenceValueFormat);
                    UpdateTestOutput();
                });
            }
        }

        /// <summary>
        /// Help text for ReferenceValueFormat.
        /// </summary>
        public string ReferenceValueFormatHelp
        {
            get
            {
                return
                    "A numeric format which is used to generate reference numbers.\n" +
                    "The result of this format must be numeric.\n" +
                    "0: System Code\n" +
                    "1: Sequence Code\n" +
                    "2: Sequence Number\n";
            }
        }

        /// <summary>
        /// A numeric format string for displaying reference numbers.
        /// </summary>
        public string ReferenceDisplayFormat
        {
            get { return _referenceDisplayFormat; }
            set
            {
                SetProperty(ref _referenceDisplayFormat, value, () => ReferenceDisplayFormat, async () =>
                {
                    await _globalFacade.SetSettingAsync("ReferenceDisplayFormat", _referenceDisplayFormat);
                    UpdateTestOutput();
                });
            }
        }

        /// <summary>
        /// Help text for ReferenceDisplayFormat.
        /// </summary>
        public string ReferenceDisplayFormatHelp
        {
            get
            {
                return
                    "A numeric format which is used to display reference numbers.\n" +
                    "0: Reference Number\n";
            }
        }

        /// <summary>
        /// A regex expression for cleaning reference numbers.
        /// </summary>
        public string ReferenceDisplayClean
        {
            get { return _referenceDisplayClean; }
            set
            {
                SetProperty(ref _referenceDisplayClean, value, () => ReferenceDisplayClean, async () =>
                {
                    await _globalFacade.SetSettingAsync("ReferenceDisplayClean", _referenceDisplayClean);
                    UpdateTestOutput();
                });
            }
        }

        /// <summary>
        /// Help text for ReferenceDisplayClean.
        /// </summary>
        public string ReferenceDisplayCleanHelp
        {
            get
            {
                return
                    "A regular expression which is used to clean formatted reference numbers.\n" +
                    "All characters which match this expression will be removed from the result of Display Format.";
            }
        }

        /// <summary>
        /// A System Code for testing reference number generation.
        /// </summary>
        public int TestSystemCode
        {
            get { return _testSystemCode; }
            set { SetProperty(ref _testSystemCode, value, () => TestSystemCode, UpdateTestOutput); }
        }

        /// <summary>
        /// A Sequence Code for testing reference number generation.
        /// </summary>
        public int TestSequenceCode
        {
            get { return _testSequenceCode; }
            set { SetProperty(ref _testSequenceCode, value, () => TestSequenceCode, UpdateTestOutput); }
        }

        /// <summary>
        /// A Sequence Number for testing reference number generation.
        /// </summary>
        public long TestSequenceNumber
        {
            get { return _testSequenceNumber; }
            set { SetProperty(ref _testSequenceNumber, value, () => TestSequenceNumber, UpdateTestOutput); }
        }

        /// <summary>
        /// The result of applying ReferenceValueFormat to TestSystemCode, TestSequenceCode and TestSequenceNumber.
        /// </summary>
        public long TestReferenceValue
        {
            get { return _testReferenceValue; }
            set { SetProperty(ref _testReferenceValue, value, () => TestReferenceValue); }
        }

        /// <summary>
        /// The result of applying ReferenceDisplayFormat to TestReferenceValue.
        /// </summary>
        public string TestReferenceDisplay
        {
            get { return _testReferenceDisplay; }
            set { SetProperty(ref _testReferenceDisplay, value, () => TestReferenceDisplay); }
        }

        /// <summary>
        /// The result of applying ReferenceDisplayClean to TestReferenceDisplay.
        /// </summary>
        public string TestReferenceDisplayCleaned
        {
            get { return _testReferenceDisplayCleaned; }
            set { SetProperty(ref _testReferenceDisplayCleaned, value, () => TestReferenceDisplayCleaned); }
        }

        #endregion
        

        #region Private Methods

        /// <summary>
        /// Loads all settings from the database into the viewmodel.
        /// </summary>
        private void LoadSettings()
        {
            // Initialize settings
            _systemCode = Convert.ToInt32(_globalFacade.GetSetting("SystemCode"));
            _referenceValueFormat = _globalFacade.GetSetting("ReferenceValueFormat");
            _referenceDisplayFormat = _globalFacade.GetSetting("ReferenceDisplayFormat");
            _referenceDisplayClean = _globalFacade.GetSetting("ReferenceDisplayClean");

            // Raise PropertyChanged events
            RaisePropertyChanged(() => SystemCode);
            RaisePropertyChanged(() => ReferenceValueFormat);
            RaisePropertyChanged(() => ReferenceDisplayFormat);
            RaisePropertyChanged(() => ReferenceDisplayClean);
        }

        /// <summary>
        /// Updates the test output values.
        /// </summary>
        private void UpdateTestOutput()
        {
            try
            {
                TestReferenceValue = ReferenceNumberHelper.FormatValue(TestSystemCode, TestSequenceCode, TestSequenceNumber, ReferenceValueFormat);
            }
            catch (Exception)
            {
                TestReferenceValue = 0;
            }

            try
            {
                TestReferenceDisplay = ReferenceNumberHelper.FormatDisplay(TestReferenceValue, ReferenceDisplayFormat);
            }
            catch (Exception)
            {
                TestReferenceDisplay = null;
            }

            try
            {
                TestReferenceDisplayCleaned = ReferenceNumberHelper.FormatDisplayCleaned(TestReferenceDisplay, ReferenceDisplayClean);
            }
            catch (Exception)
            {
                TestReferenceDisplayCleaned = null;
            }
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the GlobalFacade
                ConnectToFacade(_globalFacade);

                // Initialize the settings
                LoadSettings();
            });
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<ReferenceConfigViewModel> builder)
        {
            builder.DataFormLayout()
                .GroupBox("System Configuration")
                    .ContainsProperty(p => p.SystemCode)
                .EndGroup()
                .GroupBox("Reference Configuration")
                    .Group("Reference Value Format")
                        .ContainsProperty(p => p.ReferenceValueFormat)
                        .ContainsProperty(p => p.ReferenceValueFormatHelp)
                    .EndGroup()
                    .Group("Reference Display Format")
                        .ContainsProperty(p => p.ReferenceDisplayFormat)
                        .ContainsProperty(p => p.ReferenceDisplayFormatHelp)
                    .EndGroup()
                    .Group("Reference Display Clean")
                        .ContainsProperty(p => p.ReferenceDisplayClean)
                        .ContainsProperty(p => p.ReferenceDisplayCleanHelp)
                    .EndGroup()
                .EndGroup()
                .GroupBox("Reference Testing", Orientation.Horizontal)
                    .GroupBox("Input")
                        .ContainsProperty(p => p.TestSystemCode)
                        .ContainsProperty(p => p.TestSequenceCode)
                        .ContainsProperty(p => p.TestSequenceNumber)
                    .EndGroup()
                    .GroupBox("Output")
                        .ContainsProperty(p => p.TestReferenceValue)
                        .ContainsProperty(p => p.TestReferenceDisplay)
                        .ContainsProperty(p => p.TestReferenceDisplayCleaned);

            builder.Property(p => p.ReferenceValueFormat)
                .DisplayName("Value Format");
            builder.Property(p => p.ReferenceDisplayFormat).DisplayName("Display Format");
            builder.Property(p => p.ReferenceDisplayClean).DisplayName("Display Cleaning Expression");
            builder.Property(p => p.TestSystemCode).DisplayName("System Code");
            builder.Property(p => p.TestSequenceCode).DisplayName("Sequence Code");
            builder.Property(p => p.TestSequenceNumber).DisplayName("Sequence Number");
            builder.Property(p => p.TestReferenceValue)
                .DisplayName("Value")
                .ReadOnly();
            builder.Property(p => p.TestReferenceDisplay)
                .DisplayName("Display")
                .ReadOnly();
            builder.Property(p => p.TestReferenceDisplayCleaned)
                .DisplayName("Display Cleaned")
                .ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<ReferenceConfigViewModel> builder)
        {
            builder.Property(p => p.ReferenceValueFormat).AddValidation((value, context) =>
            {
                try
                {
                    ReferenceNumberHelper.FormatValue(0, 0, 0, (string)value);
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }

                return null;
            });

            builder.Property(p => p.ReferenceDisplayFormat).AddValidation((value, context) =>
            {
                try
                {
                    ReferenceNumberHelper.FormatDisplay(0, (string)value);
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }

                return null;
            });
            builder.Property(p => p.ReferenceDisplayClean).AddValidation((value, context) =>
            {
                try
                {
                    ReferenceNumberHelper.FormatDisplayCleaned(string.Empty, (string)value);
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }

                return null;
            });

            builder.Property(p => p.SystemCode).GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.TestSystemCode).GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.TestSequenceCode).GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.TestSequenceNumber).GetEditor<SpinEditorDefinition>().MinValue = 0;

            builder.Property(p => p.TestReferenceValue).ReplaceEditor(new SpinEditorDefinition()
            {
                MinValue = 0,
                DisplayFormat = "D0"
            });

            builder.Property(p => p.ReferenceValueFormatHelp)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();
            builder.Property(p => p.ReferenceDisplayFormatHelp)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();
            builder.Property(p => p.ReferenceDisplayCleanHelp)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();
        }

        #endregion
    }
}
