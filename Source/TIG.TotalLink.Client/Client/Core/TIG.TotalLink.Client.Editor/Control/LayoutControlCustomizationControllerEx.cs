using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.LayoutControl;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class LayoutControlCustomizationControllerEx : LayoutControlCustomizationController
    {
        #region Private Fields

        private LayoutControlCustomizationControl _externalCustomizationControl;
        private readonly PropertyInfo _selectedElementsProperty;
        private readonly PropertyInfo _selectionIndicatorsProperty;
        private readonly PropertyInfo _rootVisualProperty;
        private readonly PropertyInfo _customizationControlProperty;
        private readonly MethodInfo _showCustomizationCoverMethod;
        private readonly MethodInfo _showCustomizationCanvasMethod;
        private readonly MethodInfo _hideCustomizationToolbarMethod;
        private readonly MethodInfo _hideCustomizationCanvasMethod;
        private readonly MethodInfo _hideCustomizationCoverMethod;

        #endregion


        #region Constructors

        public LayoutControlCustomizationControllerEx(LayoutControlController controller)
            : base(controller)
        {
            // Find the type that this controller inherits from
            var baseType = GetType().BaseType;
            if (baseType == null)
                return;

            // Cache properties and methods on the base type that we will need to access via reflection
            _selectedElementsProperty = baseType.GetProperty("SelectedElements", BindingFlags.Instance | BindingFlags.Public);
            _selectionIndicatorsProperty = baseType.GetProperty("SelectionIndicators", BindingFlags.Instance | BindingFlags.NonPublic);
            _rootVisualProperty = baseType.GetProperty("RootVisual", BindingFlags.Instance | BindingFlags.NonPublic);
            _customizationControlProperty = baseType.GetProperty("CustomizationControl", BindingFlags.Instance | BindingFlags.NonPublic);
            _showCustomizationCoverMethod = baseType.GetMethod("ShowCustomizationCover", BindingFlags.Instance | BindingFlags.NonPublic);
            _showCustomizationCanvasMethod = baseType.GetMethod("ShowCustomizationCanvas", BindingFlags.Instance | BindingFlags.NonPublic);
            _hideCustomizationToolbarMethod = baseType.GetMethod("HideCustomizationToolbar", BindingFlags.Instance | BindingFlags.NonPublic);
            _hideCustomizationCanvasMethod = baseType.GetMethod("HideCustomizationCanvas", BindingFlags.Instance | BindingFlags.NonPublic);
            _hideCustomizationCoverMethod = baseType.GetMethod("HideCustomizationCover", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The external LayoutControlCustomizationControl that this controller will use for customization.
        /// </summary>
        public LayoutControlCustomizationControl ExternalCustomizationControl
        {
            get { return _externalCustomizationControl; }
            set
            {
                if (Equals(_externalCustomizationControl, value))
                    return;

                _externalCustomizationControl = value;
            }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Sets the value of the SelectedElements property on the base class.
        /// </summary>
        private FrameworkElements SelectedElementsInternal
        {
            set { _selectedElementsProperty.SetValue(this, value); }
        }

        /// <summary>
        /// Sets the value of the SelectionIndicators property on the base class.
        /// </summary>
        private LayoutItemSelectionIndicators SelectionIndicatorsInternal
        {
            set { _selectionIndicatorsProperty.SetValue(this, value); }
        }

        /// <summary>
        /// Sets the value of the RootVisual property on the base class.
        /// </summary>
        private Window RootVisualInternal
        {
            set { _rootVisualProperty.SetValue(this, value); }
        }

        /// <summary>
        /// Sets the value of the CustomizationControl property on the base class.
        /// </summary>
        private LayoutControlCustomizationControl CustomizationControlInternal
        {
            set { _customizationControlProperty.SetValue(this, value); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Invokes the ShowCustomizationCover method on the base class.
        /// </summary>
        private void ShowCustomizationCoverInternal()
        {
            _showCustomizationCoverMethod.Invoke(this, null);
        }

        /// <summary>
        /// Invokes the ShowCustomizationCanvas method on the base class.
        /// </summary>
        private void ShowCustomizationCanvasInternal()
        {
            _showCustomizationCanvasMethod.Invoke(this, null);
        }

        /// <summary>
        /// Invokes the HideCustomizationToolbar method on the base class.
        /// </summary>
        /// <param name="remove">Indicates if the toolbar should be removed.</param>
        private void HideCustomizationToolbarInternal(bool remove)
        {
            _hideCustomizationToolbarMethod.Invoke(this, new object[] { remove });
        }

        /// <summary>
        /// Invokes the HideCustomizationCanvas method on the base class.
        /// </summary>
        private void HideCustomizationCanvasInternal()
        {
            _hideCustomizationCanvasMethod.Invoke(this, null);
        }

        /// <summary>
        /// Invokes the HideCustomizationCover method on the base class.
        /// </summary>
        private void HideCustomizationCoverInternal()
        {
            _hideCustomizationCoverMethod.Invoke(this, null);
        }

        /// <summary>
        /// Shows the customization control.
        /// </summary>
        private void ShowCustomizationControl()
        {
            CustomizationControlInternal = CreateCustomizationControl();
            InitCustomizationControl();
        }

        /// <summary>
        /// Hides the customization control.
        /// </summary>
        private void HideCustomizationControl()
        {
            FinalizeCustomizationControl();
            CustomizationControlInternal = null;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the AvailableItemsChanged event on the LayoutControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void OnAvailableItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnAvailableItemsChanged(e);
        }

        /// <summary>
        /// Handles the StartAvailableItemDragAndDrop on the CustomizationControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CustomizationControl_StartAvailableItemDragAndDrop(object sender, LayoutControlStartDragAndDropEventArgs<FrameworkElement> e)
        {
            StartAvailableItemDragAndDrop(e);
        }

        /// <summary>
        /// Handles the StartNewItemDragAndDrop on the CustomizationControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CustomizationControl_StartNewItemDragAndDrop(object sender, LayoutControlStartDragAndDropEventArgs<LayoutControlNewItemInfo> e)
        {
            StartNewItemDragAndDrop(e);
        }

        #endregion


        #region Overrides

        protected override void InitCustomizationControl()
        {
            CustomizationControl.AvailableItems = ILayoutControl.VisibleAvailableItems;
            CustomizationControl.NewItemsInfo = GetNewItemsInfo();
            CustomizationControl.Style = CustomizationControlStyle;

            CustomizationControl.DeleteAvailableItem += DeleteAvailableItem;
            CustomizationControl.StartAvailableItemDragAndDrop += CustomizationControl_StartAvailableItemDragAndDrop;
            CustomizationControl.StartNewItemDragAndDrop += CustomizationControl_StartNewItemDragAndDrop;
        }

        protected override void FinalizeCustomizationControl()
        {
            CustomizationControl.DeleteAvailableItem -= DeleteAvailableItem;
            CustomizationControl.StartAvailableItemDragAndDrop -= CustomizationControl_StartAvailableItemDragAndDrop;
            CustomizationControl.StartNewItemDragAndDrop -= CustomizationControl_StartNewItemDragAndDrop;

            CustomizationControl.AvailableItems = null;
            CustomizationControl.NewItemsInfo = null;
        }

        protected override LayoutControlCustomizationControl CreateCustomizationControl()
        {
            //System.Diagnostics.Debug.WriteLine("CreateCustomizationControl");

            // If an ExternalCustomizationControl has been set, return it
            if (ExternalCustomizationControl != null)
                return ExternalCustomizationControl;

            // Otherwise call the base class to create one
            return base.CreateCustomizationControl();
        }

        protected override void BeginCustomization()
        {
            SelectedElementsInternal = new FrameworkElements();
            SelectedElements.CollectionChanged += (sender, e) => OnSelectedElementsChanged(e);

            if (!Control.IsInDesignTool())
                ShowCustomizationCoverInternal();

            ShowCustomizationCanvasInternal();

            if (!Control.IsInDesignTool())
            {
                SelectionIndicatorsInternal = CreateSelectionIndicators(CustomizationCanvas);
                SelectionIndicators.ItemStyle = ItemSelectionIndicatorStyle;
                ShowCustomizationControl();

//#if !SILVERLIGHT
                if (Controller.IsLoaded)
                    RootVisualInternal = Control.FindElementByTypeInParents<Window>(null);
//#endif
            }

            ILayoutControl.AvailableItems.CollectionChanged += OnAvailableItemsChanged;
        }

        protected override void EndCustomization()
        {
            ILayoutControl.AvailableItems.CollectionChanged -= OnAvailableItemsChanged;

//#if !SILVERLIGHT
            RootVisualInternal = null;
//#endif
            SelectedElements.Clear();

            if (CustomizationToolbar != null)
                HideCustomizationToolbarInternal(true);

            if (!Control.IsInDesignTool())
                HideCustomizationControl();

            SelectionIndicatorsInternal = null;
            HideCustomizationCanvasInternal();

            if (!Control.IsInDesignTool())
                HideCustomizationCoverInternal();

            SelectedElementsInternal = null;
        }

        #endregion
    }
}
