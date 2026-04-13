using System;
using NUnit.Framework;
using TIG.IntegrationServer.Common.ConcurrentCollection;

namespace TIG.IntegrationServer.Test.Unit.SyncEngine.BlockingQueue
{
    [TestFixture]
    public class BlockingQueueNonParallelTest
    {
        private BlockingQueue<object> _queue;
        private const int Limit = 1000;

        [SetUp]
        public void Init()
        {
            _queue = new BlockingQueue<object>(Limit);
        }

        [Test]
        public void Enqueue([Values(1, Limit / 2, Limit)] int itemsNumber)
        {
            var initCount = _queue.Count;
            for (ushort i = 1; i <= itemsNumber; i++)
                _queue.Enqueue(new Object());
            Assert.That(_queue.Count, Is.EqualTo(initCount + itemsNumber),
                "Number of queued items at the end of the test isn't equal to sum of items queued at the begining and added during the test.");
        }

        [Test]
        public void EnqueueUpToLimit()
        {
            Enqueue(_queue.Limit - _queue.Count);
            Assert.That(_queue.Count, Is.EqualTo(Limit), "Queue isn't full.");
        }

        [Test]
        public void Clear()
        {
            _queue.Clear();
            Assert.That(_queue.Count, Is.EqualTo(0), "Queue isn't empty.");
            EnqueueUpToLimit();
            _queue.Clear();
            Assert.That(_queue.Count, Is.EqualTo(0), "Queue isn't empty.");
        }

        private void Dequeue(int itemsNumber)
        {
            if (itemsNumber > _queue.Count)
                throw new ApplicationException("Unit test Error. Attemting to dequeue item from empty queue.");
            var initCount = _queue.Count;
            for (ushort i = 1; i <= itemsNumber; i++)
                _queue.Dequeue();
            Assert.That(_queue.Count, Is.EqualTo(initCount - itemsNumber),
                "Number of queued items at the end of the test isn't equal to inequality of items queued at the begining and dequeued during the test.");
        }

        [Test]
        public void TryDequeueAll([Values(1, Limit / 2, Limit)] int itemsNumber)
        {
            Clear();
            Enqueue(itemsNumber);
            object dummy;
            var count = 0;
            while (_queue.TryDequeue(out dummy))
                count++;
            Assert.That(_queue.Count, Is.EqualTo(0), "Queue isn't empty.");
            Assert.That(count, Is.EqualTo(itemsNumber), "Actual number of dequeued items isn't equal to required.");
        }

        [Test]
        public void ComplexTest()
        {
            Enqueue(Limit / 3);
            EnqueueUpToLimit();
            Dequeue(3 * Limit / 4);
            Clear();
            TryDequeueAll(Limit / 5);
            Clear();
        }
        
        [TearDown]
        public void CleanUp()
        {
            _queue = null;
        }
    }
}