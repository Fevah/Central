using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Extension;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class CheckedListBox : ListBox
    {
        #region Dependency Properties

        public static readonly DependencyProperty CheckedItemsProperty =
                DependencyProperty.Register("CheckedItems", typeof(IList), typeof(CheckedListBox), new PropertyMetadata((s, e) => ((CheckedListBox)s).OnCheckedItemsChanged(e)));

        /// <summary>
        /// A list of all the checked items.
        /// </summary>
        public IList CheckedItems
        {
            get { return (IList)GetValue(CheckedItemsProperty); }
            set { SetValue(CheckedItemsProperty, value); }
        }

        #endregion


        #region Private Fields

        private NotifyCollectionChangedEventHandler _checkedItemsCollectionChangedHandler;
        private bool _syncingCollections;

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the value of CheckedItems changes
        /// </summary>
        /// <param name="e">Event arguments.</param>
        private void OnCheckedItemsChanged(DependencyPropertyChangedEventArgs e)
        {
            // Stop handling the CollectionChanged event on the old value
            if (e.OldValue != null && _checkedItemsCollectionChangedHandler != null && typeof(ObservableCollection<>).IsAssignableFromGeneric(e.OldValue.GetType()))
            {
                var collectionChangedEvent = e.OldValue.GetType().GetEvent("CollectionChanged");
                if (collectionChangedEvent == null)
                {
                    _checkedItemsCollectionChangedHandler = null;
                    return;
                }

                var removeMethod = collectionChangedEvent.GetRemoveMethod();
                removeMethod.Invoke(e.OldValue, new object[] { _checkedItemsCollectionChangedHandler });
                _checkedItemsCollectionChangedHandler = null;
            }

            if (e.NewValue != null)
            {
                // Start handling the CollectionChanged event on the new value
                if (typeof(ObservableCollection<>).IsAssignableFromGeneric(e.NewValue.GetType()))
                {
                    var collectionChangedEvent = e.NewValue.GetType().GetEvent("CollectionChanged");
                    if (collectionChangedEvent == null)
                        return;

                    _checkedItemsCollectionChangedHandler = CheckedItems_CollectionChanged;
                    var addMethod = collectionChangedEvent.GetAddMethod();
                    addMethod.Invoke(e.NewValue, new object[] {_checkedItemsCollectionChangedHandler});
                }

                // Sync the initial CheckedItems to the SelectedItems
                _syncingCollections = true;
                CheckedItems.SyncTo(SelectedItems);
                _syncingCollections = false;
            }
        }

        /// <summary>
        /// Handles the CheckedItems.CollectionChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CheckedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Abort if we are already syncing the collections
            if (_syncingCollections)
                return;

            // Sync the CheckedItems to the SelectedItems
            _syncingCollections = true;
            CheckedItems.SyncTo(SelectedItems);
            _syncingCollections = false;
        }

        #endregion


        #region Overrides

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            // Abort if we are already syncing the collections
            if (_syncingCollections)
                return;

            // Sync the SelectedItems to the CheckedItems
            _syncingCollections = true;
            SelectedItems.SyncTo(CheckedItems);
            _syncingCollections = false;
        }

        #endregion
    }
}
