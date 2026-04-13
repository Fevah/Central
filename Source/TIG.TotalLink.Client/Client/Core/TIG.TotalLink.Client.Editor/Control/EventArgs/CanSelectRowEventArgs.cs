namespace TIG.TotalLink.Client.Editor.Control.EventArgs
{
    public class CanSelectRowEventArgs : System.EventArgs
    {
        #region Constructors

        public CanSelectRowEventArgs(int oldRowHandle, int newRowHandle)
        {
            OldRowHandle = oldRowHandle;
            NewRowHandle = newRowHandle;
            Allow = true;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The current row handle that is selected.
        /// </summary>
        public int OldRowHandle { get; private set; }

        /// <summary>
        /// The new row handle that will be selected.
        /// </summary>
        public int NewRowHandle { get; private set; }

        /// <summary>
        /// Indicates if the NewRowHandle is allowed to be selected.
        /// </summary>
        public bool Allow { get; set; }

        #endregion
    }
}
