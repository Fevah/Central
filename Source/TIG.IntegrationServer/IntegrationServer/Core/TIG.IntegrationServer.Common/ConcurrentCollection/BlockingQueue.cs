using System;
using System.Threading;

namespace TIG.IntegrationServer.Common.ConcurrentCollection
{
    public class BlockingQueue<T>
    {
        #region Nested Classes

        private class BlockingQueueBase<T>
        {
            #region Nested Classes

            private class BlockingQueueBaseNode<T>
            {
                #region Private Fields

                private readonly T _item;

                #endregion


                #region Constructors

                /// <summary>
                /// Consturctor with item
                /// </summary>
                /// <param name="item">Item want in queue</param>
                public BlockingQueueBaseNode(T item)
                {
                    _item = item;
                }

                #endregion


                #region Public Properties

                /// <summary>
                /// Item in queue
                /// </summary>
                public T Item
                {
                    get { return _item; }
                }

                /// <summary>
                /// Next item
                /// </summary>
                public BlockingQueueBaseNode<T> Next
                {
                    get;
                    set;
                }

                #endregion
            }

            #endregion


            #region Private Fields

            private BlockingQueueBaseNode<T> _first;
            private BlockingQueueBaseNode<T> _last;
            private readonly int _limit;

            #endregion


            #region Constructors

            /// <summary>
            /// Consturctor with limt
            /// </summary>
            /// <param name="limit">Limit for this queue</param>
            public BlockingQueueBase(int limit)
            {
                if (limit <= 0)
                    throw new ArgumentOutOfRangeException("limit", limit, "Queue items limit can't be nil or negative.");
                _limit = limit;
            }

            #endregion


            #region Public Properties

            /// <summary>
            /// Limit for queue items limit
            /// </summary>
            public int Limit
            {
                get
                {
                    return _limit;
                }
            }

            /// <summary>
            /// Queue is empty
            /// </summary>
            public bool IsEmpty
            {
                get
                {
                    return _first == null;
                }
            }

            /// <summary>
            /// Count of queue items
            /// </summary>
            public int Count { get; private set; }

            public bool IsFull
            {
                get
                {
                    return Count == _limit;
                }
            }

            #endregion


            #region Public Methods

            /// <summary>
            /// Enqueue item
            /// </summary>
            /// <param name="newItem">To be en quene</param>
            public void Enqueue(T newItem)
            {
                if (IsFull)
                {
                    throw new InvalidOperationException("Attemting to enqueue item into full queue.");
                }

                var newNode = new BlockingQueueBaseNode<T>(newItem);

                if (IsEmpty)
                {
                    _first = newNode;
                }
                else
                {
                    _last.Next = newNode;
                }

                _last = newNode;
                Count++;
            }

            /// <summary>
            /// Dequeue for get first queue item
            /// </summary>
            /// <returns>item</returns>
            public T Dequeue()
            {
                if (IsEmpty)
                {
                    throw new InvalidOperationException("Attemting to dequeue item from empty queue.");
                }

                var resultNode = _first;
                _first = _first.Next;

                if (_first == null)
                {
                    _last = null;
                }

                resultNode.Next = null;
                Count--;

                return resultNode.Item;
            }

            public void Clear()
            {
                while (!IsEmpty)
                {
                    Dequeue();
                }
            }

            #endregion
        }

        #endregion


        #region Private Fields

        private readonly BlockingQueueBase<T> _queue;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public BlockingQueue()
        {
            _queue = new BlockingQueueBase<T>(int.MaxValue);
        }

        /// <summary>
        /// Constuctor with item limit
        /// </summary>
        /// <param name="itemsLimit">Item limit</param>
        public BlockingQueue(int itemsLimit)
        {
            _queue = new BlockingQueueBase<T>(itemsLimit);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Limit for indicate how many items can be add to queue
        /// </summary>
        public int Limit
        {
            get
            {
                return _queue.Limit;
            }
        }

        /// <summary>
        /// Count of queue items
        /// </summary>
        public int Count
        {
            get
            {
                int count;
                lock (_queue)
                {
                    count = _queue.Count;
                }
                return count;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Enqueue item
        /// </summary>
        /// <param name="newItem">To be en quene</param>
        public void Enqueue(T newItem)
        {
            lock (_queue)
            {
                while (_queue.IsFull)
                {
                    Monitor.Wait(_queue);
                }
                _queue.Enqueue(newItem);
                Monitor.Pulse(_queue);
            }
        }

        /// <summary>
        /// Dequeue for get first queue item
        /// </summary>
        /// <returns>item</returns>
        public T Dequeue()
        {
            T result;
            lock (_queue)
            {
                while (_queue.IsEmpty)
                {
                    Monitor.Wait(_queue);
                }
                result = _queue.Dequeue();
                Monitor.Pulse(_queue);
            }
            return result;
        }

        /// <summary>
        /// Try dequeue for get first queue item test
        /// </summary>
        /// <returns>True, indicate can be dequeue</returns>
        public bool TryDequeue(out T item)
        {
            var result = false;
            lock (_queue)
            {
                if (_queue.IsEmpty)
                    item = default(T);
                else
                {
                    item = _queue.Dequeue();
                    result = true;
                    Monitor.Pulse(_queue);
                }
            }
            return result;
        }

        /// <summary>
        /// Clear queue items
        /// </summary>
        public void Clear()
        {
            lock (_queue)
            {
                _queue.Clear();
                Monitor.PulseAll(_queue);
            }
        }

        #endregion
    }
}
