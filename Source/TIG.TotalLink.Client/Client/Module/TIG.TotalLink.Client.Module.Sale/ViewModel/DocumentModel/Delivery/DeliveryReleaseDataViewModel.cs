using System.Collections.ObjectModel;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.DataModel.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel.Delivery
{
    public class DeliveryReleaseDataViewModel : LocalDataObjectBase
    {
        #region Public Enums

        public enum ReleaseStatuses
        {
            None,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/Delivery/Dispatch.png")]
            [EnumToolTip("This delivery will be marked as dispatched.")]
            Dispatch,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/Delivery/CantDispatch.png")]
            [EnumToolTip("This delivery is not ready to be dispatched.")]
            CantDispatch,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/Delivery/CantDispatch.png")]
            [EnumToolTip("This delivery has already been dispatched.")]
            Dispatched
        }

        #endregion


        #region Private Fields

        private Shared.DataModel.Sale.Delivery _delivery;
        private Contact _contact;
        private DeliveryStatus _deliveryStatus;
        private string _consignmentNote;
        private ReleaseStatuses _status;
        private bool _hasError;
        private readonly ObservableCollection<PickItemReleaseDataViewModel> _pickItems = new ObservableCollection<PickItemReleaseDataViewModel>();

        #endregion


        #region Public Properties

        /// <summary>
        /// The Delivery that this data represents.
        /// </summary>
        public Shared.DataModel.Sale.Delivery Delivery
        {
            get { return _delivery; }
            set { SetProperty(ref _delivery, value, () => Delivery); }
        }

        /// <summary>
        /// The PickItemData that belongs to this data.
        /// </summary>
        public ObservableCollection<PickItemReleaseDataViewModel> PickItems
        {
            get { return _pickItems; }
        }

        /// <summary>
        /// The Contact for the delivery.
        /// </summary>
        public Contact Contact
        {
            get { return _contact; }
            set { SetProperty(ref _contact, value, () => Contact); }
        }

        /// <summary>
        /// The Status of the delivery.
        /// </summary>
        public DeliveryStatus DeliveryStatus
        {
            get { return _deliveryStatus; }
            set { SetProperty(ref _deliveryStatus, value, () => DeliveryStatus); }
        }

        /// <summary>
        /// The con note for the delivery.
        /// </summary>
        public string ConsignmentNote
        {
            get { return _consignmentNote; }
            set
            {
                SetProperty(ref _consignmentNote, value, () => ConsignmentNote,
                    UpdateStatus
                );
            }
        }

        /// <summary>
        /// The status of this delivery data.
        /// </summary>
        public ReleaseStatuses Status
        {
            get { return _status; }
            private set
            {
                SetProperty(ref _status, value, () => Status, () =>
                {
                    HasError = (Status == ReleaseStatuses.CantDispatch);
                });
            }
        }

        /// <summary>
        /// Indicates if this ReleaseItem has any errors.
        /// </summary>
        public bool HasError
        {
            get { return _hasError; }
            private set { SetProperty(ref _hasError, value, () => HasError); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Updates the status based on the quantities.
        /// </summary>
        public void UpdateStatus()
        {
            if (!string.IsNullOrWhiteSpace(Delivery.ConsignmentNote))
            {
                Status = ReleaseStatuses.Dispatched;
                return;
            }

            if (string.IsNullOrWhiteSpace(ConsignmentNote))
            {
                Status = ReleaseStatuses.None;
                return;
            }

            if (!DeliveryStatus.CanBeDispatched)
            {
                Status = ReleaseStatuses.CantDispatch;
                return;
            }

            Status = ReleaseStatuses.Dispatch;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DeliveryReleaseDataViewModel> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Status)
                .ContainsProperty(p => p.Delivery)
                .ContainsProperty(p => p.Contact)
                .ContainsProperty(p => p.DeliveryStatus)
                .ContainsProperty(p => p.ConsignmentNote);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Status)
                    .ContainsProperty(p => p.Delivery)
                    .ContainsProperty(p => p.Contact)
                    .ContainsProperty(p => p.DeliveryStatus)
                    .ContainsProperty(p => p.ConsignmentNote);

            builder.Property(p => p.Status)
                .DisplayName("")
                .ReadOnly();
            builder.Property(p => p.Delivery).ReadOnly();
            builder.Property(p => p.DeliveryStatus).DisplayName("Status");
            builder.Property(p => p.Contact).ReadOnly();
            builder.Property(p => p.DeliveryStatus).ReadOnly();
            builder.Property(p => p.HasError).NotAutoGenerated();
            builder.Property(p => p.PickItems).NotAutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DeliveryReleaseDataViewModel> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Delivery);

            builder.Property(p => p.Contact)
                .ColumnWidth(200);
            builder.Property(p => p.DeliveryStatus)
                .ColumnWidth(200);
            builder.Property(p => p.ConsignmentNote)
                .ColumnWidth(200);
            builder.Property(p => p.Status)
                .ReplaceEditor(new ComboEditorDefinition(typeof(ReleaseStatuses))
                {
                    AllowPopup = false,
                    DisplayMode = EnumEditorDefinitionBase.DisplayModes.ImageOnly
                })
                .FixedWidth()
                .ColumnWidth(50);
        }

        #endregion
    }
}
