using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace TIG.IntegrationServer.Common.ConcurrentCollection
{
    public class ConcurrentHashSet<T> : ICollection<T>, IDisposable
    {
        #region Private Fields

        private readonly HashSet<T> _hashSet = new HashSet<T>();
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private bool _disposed;

        #endregion


        #region Public Methods

        /// <summary>
        /// Try add item to hash set
        /// </summary>
        /// <param name="item">item to add to hash set</param>
        /// <returns></returns>
        public bool TryAdd(T item)
        {
            try
            {
                _rwLock.EnterWriteLock();
                return _hashSet.Add(item);
            }
            finally
            {
                if (_rwLock.IsWriteLockHeld)
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }

        #endregion


        #region ICollection<T> Members

        /// <summary>
        /// Add item to hash set
        /// </summary>
        /// <param name="item">Item to be add</param>
        public void Add(T item)
        {
            TryAdd(item);
        }

        /// <summary>
        /// Clear all item from hashset
        /// </summary>
        public void Clear()
        {
            try
            {
                _rwLock.EnterWriteLock();
                _hashSet.Clear();
            }
            finally
            {
                if (_rwLock.IsWriteLockHeld)
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Test item in hashset or not
        /// </summary>
        /// <param name="item">Item to be test</param>
        /// <returns>True, indicate item in hash set</returns>
        public bool Contains(T item)
        {
            try
            {
                _rwLock.EnterReadLock();
                return _hashSet.Contains(item);
            }
            finally
            {
                if (_rwLock.IsReadLockHeld)
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove item from hash set
        /// </summary>
        /// <param name="item">Item to be remove</param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            try
            {
                _rwLock.EnterWriteLock();
                return _hashSet.Remove(item);
            }
            finally
            {
                if (_rwLock.IsWriteLockHeld)
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Count indicate how many items in hashset
        /// </summary>
        public int Count
        {
            get
            {
                try
                {
                    _rwLock.EnterReadLock();
                    return _hashSet.Count;
                }
                finally
                {
                    if (_rwLock.IsReadLockHeld)
                    {
                        _rwLock.ExitReadLock();
                    }
                }
            }
        }

        /// <summary>
        /// IsReadOnly indicate current hash set is read only.
        /// </summary>
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        #endregion


        #region IEnumerable<out T> Members

        /// <summary>
        /// GetEnumerator<T> for converter item to IEnumerator<T>
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            try
            {
                _rwLock.EnterReadLock();
                return _hashSet.GetEnumerator();
            }
            finally
            {
                if (_rwLock.IsReadLockHeld)
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

        #endregion


        #region IEnumerable Members

        /// <summary>
        /// GetEnumerator for converter item to IEnumerator
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion


        #region Dispose Pattern

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ConcurrentHashSet()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_rwLock != null)
                {
                    _rwLock.Dispose();
                }
            }

            _disposed = true;
        }

        #endregion
    }
}
