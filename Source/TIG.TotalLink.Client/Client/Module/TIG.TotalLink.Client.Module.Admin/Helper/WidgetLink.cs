using System;
using System.Windows;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class WidgetLink
    {
        #region Private Fields

        private PanelViewModel _panel;
        private string _name;
        private Guid? _panelOid;

        #endregion


        #region Constructors

        public WidgetLink(PanelViewModel panel)
        {
            _panel = panel;
        }

        public WidgetLink(string name, Guid panelOid)
        {
            _name = name;

            if (panelOid != Guid.Empty)
                _panelOid = panelOid;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if this link is broken because it references a panel that no longer exists.
        /// </summary>
        public bool IsBroken
        {
            get { return (_panel == null); }
        }

        /// <summary>
        /// Returns the Oid for the panel this link refers to.
        /// If the link is broken this will return null.
        /// </summary>
        public string LinkId
        {
            get { return (IsBroken ? (_panelOid.HasValue ? _panelOid.Value.ToString("B") : null) : _panel.Oid.ToString("B")); }
        }

        /// <summary>
        /// The name of the panel this link refers to.
        /// </summary>
        public string Name
        {
            get
            {
                return (!IsBroken
                    ? (!string.IsNullOrWhiteSpace(_panel.ToString()) ? _panel.ToString() : "(no name)")
                    : _name);
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Break this link by clearing its panel reference.
        /// </summary>
        /// <returns>True if the link was broken; otherwise false.</returns>
        public bool Break()
        {
            // Abort if the link is already broken
            if (IsBroken)
                return false;

            // Store the current panel details, and clear the reference to the panel
            _name = _panel.ToString();
            _panelOid = _panel.Oid;
            _panel = null;

            return true;
        }

        /// <summary>
        /// Re-attaches a broken link.
        /// </summary>
        /// <param name="panel">
        /// The panel to attach to.
        /// The Oid of this panel must match the Oid of the panel the link was originally attached to.
        /// </param>
        /// <returns>True if the link was re-attached; otherwise false.</returns>
        public bool ReAttach(PanelViewModel panel)
        {
            // Abort if the link is not broken
            if (!IsBroken)
                return false;

            // Abort if the panel Oid does not match the panel this link was originally attached to
            if (!_panelOid.HasValue || _panelOid.Value != panel.Oid)
                return false;

            // Re-attach the reference to the panel, and clear the stored panel details
            _panel = panel;
            _name = null;
            _panelOid = null;

            return true;
        }

        /// <summary>
        /// Indicates if this link refers to the specified panel Oid.
        /// </summary>
        /// <param name="panelOid">The Oid of the panel to test.</param>
        /// <returns>True if this link refers to the specific panel Oid; otherwise false.</returns>
        public bool IsLinkedTo(Guid panelOid)
        {
            // If the link is broken, test against the stored panel Oid
            if (IsBroken)
                return (_panelOid.HasValue && _panelOid.Value == panelOid);

            // If the link is not broken, test against the referenced panel Oid
            return (_panel.Oid == panelOid);
        }

        /// <summary>
        /// Indicates if this link refers to the specified panel.
        /// </summary>
        /// <param name="panel">The panel to test.</param>
        /// <returns>True if this link refers to the specific panel; otherwise false.</returns>
        public bool IsLinkedTo(PanelViewModel panel)
        {
            if (panel == null)
                return false;

            return IsLinkedTo(panel.Oid);
        }

        /// <summary>
        /// Gets the widget that this link represents.
        /// </summary>
        /// <returns>The widget that this link represents.</returns>
        public WidgetViewModelBase GetWidget()
        {
            // Abort if the link is broken
            if (IsBroken)
                return null;

            // Attempt to get the panel content as a FrameworkElement
            var element = _panel.Content as FrameworkElement;
            if (element == null)
                return null;

            // Return the element DataContext as a WidgetViewModelBase
            return element.DataContext as WidgetViewModelBase;
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return (!IsBroken ? Name : string.Format("*{0}*", _name));
        }

        #endregion
    }
}
