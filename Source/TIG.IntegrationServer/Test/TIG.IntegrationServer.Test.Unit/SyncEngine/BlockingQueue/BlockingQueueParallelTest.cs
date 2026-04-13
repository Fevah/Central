using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TIG.IntegrationServer.Common.ConcurrentCollection;

namespace TIG.IntegrationServer.Test.Unit.SyncEngine.BlockingQueue
{
    [TestFixture]
    public class BlockingQueueParallelTests
    {
        private abstract class ItemContainer
        {
            protected Stack<object> Items;
        }


        private class ItemProducer : ItemContainer
        {
            public ItemProducer(int amount)
            {
                Items = new Stack<object>();
                for (var i = 1; i <= amount; i++)
                    Items.Push(new Object());
            }

            public bool IsEmpty
            {
                get
                {
                    bool result;
                    lock (Items)
                        result = (Items.Count == 0);
                    return result;
                }
            }

            public bool TryProduce(out object item)
            {
                var result = false;
                lock (Items)
                {
                    if (IsEmpty)
                        item = null;
                    else 
                    {
                        item = Items.Pop();
                        result = true;
                    }
                }
                return result;
            }
        }


        private class ItemConsumer : ItemContainer
        {
            public ItemConsumer()
            {
                Items = new Stack<object>();
            }

            public int Count
            {
                get
                {
                    int result;
                    lock (Items)
                        result = Items.Count;
                    return result;
                }
            }

            public void Consume(object item)
            {
                lock (Items)
                    Items.Push(item);
            }
        }


        private BlockingQueue<object> _queue;
        private const int Limit = 1000;
        private ItemProducer _producer;
        private ItemConsumer _consumer;

        [SetUp]
        public void Init()
        {
            _producer = new ItemProducer(10000);
            _queue = new BlockingQueue<object>(Limit);
            _consumer = new ItemConsumer();
        }

        [Test, Timeout(3000)]
        public void EnqueueingIntoFull()
        {
            for (var i = 1; i <= _queue.Limit; i++)
                _queue.Enqueue(new Object());

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(2000);
                _queue.Dequeue();
            });

            _queue.Enqueue(new Object());
            Assert.That(_queue.Count, Is.EqualTo(Limit), "Queue must be full at the end of the test. But it is not.");
        }

        [Test, Timeout(3000)]
        public void DequeueingFromEmpty()
        {
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(2000);
                _queue.Enqueue(new Object());
            });

            _queue.Dequeue();
            Assert.That(_queue.Count, Is.EqualTo(0), "Queue must be empty at the end of the test. But it is not.");
        }

        [Test]
        [TestCase(10,10,100,1)]
        public void ProducingStartsBeforeConsuming(
            int produceWorkersNum,            
            int consumeWorkersNum,
            int itemsTotal,
            int queueLimit)
        {
            _producer = new ItemProducer(itemsTotal);
            _queue = new BlockingQueue<object>(queueLimit);

            for (var i = 1; i <= produceWorkersNum; i++)
                Task.Factory.StartNew(ProduceWorkerTask);

            var consumeTasks = new List<Task>();
            for (var i = 1; i <= consumeWorkersNum; i++)
            {
                var task = new Task(() => ConsumeWorkerTask(itemsTotal));
                consumeTasks.Add(task);
                task.Start();
            }

            foreach (var consumeTask in consumeTasks)
                consumeTask.Wait();
            Assert.That(_producer.IsEmpty, Is.True, "Produser object still contains items.");
            Assert.That(_queue.Count, Is.EqualTo(0), "Queue still contains items.");
            Assert.That(_consumer.Count, Is.EqualTo(itemsTotal), "Not all the items were consumed.");
        }

        private void ProduceWorkerTask()
        {
            object item;
            while (_producer.TryProduce(out item))
                _queue.Enqueue(item);
        }

        private void ConsumeWorkerTask(int itemsTotal)
        {
            while (_consumer.Count < itemsTotal)
            {
                object item;
                while (_queue.TryDequeue(out item))
                    _consumer.Consume(item);
                Thread.Sleep(100);
            }
        }

        [Test]
        [TestCase(10, 10, 100, 1)]
        public void ConsumingStartsBeforeProducing(
            int produceWorkersNum,
            int consumeWorkersNum,
            int itemsTotal,
            int queueLimit)
        {
            _producer = new ItemProducer(itemsTotal);
            _queue = new BlockingQueue<object>(queueLimit);

            var consumeTasks = new List<Task>();
            for (var i = 1; i <= consumeWorkersNum; i++)
            {
                var task = new Task(() => ConsumeWorkerTask(itemsTotal));
                consumeTasks.Add(task);                
            }
            foreach (var consumeTask in consumeTasks)
                consumeTask.Start();

            for (var i = 1; i <= produceWorkersNum; i++)
                Task.Factory.StartNew(ProduceWorkerTask);

            foreach (var consumeTask in consumeTasks)
                consumeTask.Wait();
            Assert.That(_producer.IsEmpty, Is.True, "Produser object still contains items.");
            Assert.That(_queue.Count, Is.EqualTo(0), "Queue still contains items.");
            Assert.That(_consumer.Count, Is.EqualTo(itemsTotal), "Not all the items were consumed.");
        }
         
        [TearDown]
        public void CleanUp()
        {
            _queue = null;
            _producer = null;
            _consumer = null;
        }
    }
}
