using System;

namespace TIG.IntegrationServer.SyncEngine.Core.Event
{
    public abstract class SyncEngineEvent<TEventArgs, TPublisher>
        where TEventArgs : SyncEngineEvent<TEventArgs, TPublisher>.Args
    {
        public abstract class Args : EventArgs
        {
            #region Public Properties

            public TPublisher Publisher { get; private set; }

            #endregion


            #region Constructors

            /// <summary>
            /// Consturctor with publisher
            /// </summary>
            /// <param name="publisher">Publisher, who send this message.</param>
            protected Args(TPublisher publisher)
            {
                Publisher = publisher;
            }

            #endregion
        }
    }
}
