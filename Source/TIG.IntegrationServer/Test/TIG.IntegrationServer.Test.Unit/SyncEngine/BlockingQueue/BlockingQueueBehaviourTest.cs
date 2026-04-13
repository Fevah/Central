using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rhino.Mocks;
using TIG.IntegrationServer.Common.ConcurrentCollection;

namespace TIG.IntegrationServer.Test.Unit.SyncEngine.BlockingQueue
{
    [TestFixture]
    public class BlockingQueueBehaviourTest
    {
        private BlockingQueue<object> _queue;
        private MockRepository _mockRepository;
        private IProducer _producer;
        private IConsumer _consumer;

        [SetUp]
        public void Init()
        {
            _mockRepository = new MockRepository();
            _producer = _mockRepository.StrictMock<IProducer>();
            _consumer = _mockRepository.StrictMock<IConsumer>();
        }

        [Test]
        [TestCase(10, 10, 10, 1)]
        public void BehaviorTest(
            int produceWorkersNum,            
            int consumeWorkersNum,
            int itemsPerProduseWorker,
            int queueLimit)
        {
            _queue = new BlockingQueue<object>(queueLimit);
            var totalItems = itemsPerProduseWorker*produceWorkersNum;

            var workersTasks = new List<Task>();
            for (var i = 1; i <= produceWorkersNum; i++)
                workersTasks.Add(new Task(() => ProduceWorkerTask(itemsPerProduseWorker)));
            for (var i = 1; i <= consumeWorkersNum; i++)
                workersTasks.Add(new Task(ConsumeWorkerTask));

            Expect.Call(_producer.ProduceItem())
                .Return(new Object())
                .Repeat.Times(totalItems)
                .Message("Produced items number isn't equal to " + totalItems);

            Expect.Call(_consumer.Consume(1))
                .IgnoreArguments()
                .Return(true)
                .Repeat.Times(totalItems)
                .Message("Consumed items number isn't equal to " + totalItems);
            
            _mockRepository.ReplayAll();
            foreach (var workersTask in workersTasks)
                workersTask.Start();
            foreach (var workersTask in workersTasks)
                workersTask.Wait();

            _mockRepository.VerifyAll();
            Assert.That(_queue.Count, Is.EqualTo(0), "Queue still contains items.");
        }

        private void ProduceWorkerTask(int itemsToProduse)
        {
            for (var i = 1; i <= itemsToProduse; i++)
                _queue.Enqueue(_producer.ProduceItem());
        }

        private void ConsumeWorkerTask()
        {
            Thread.Sleep(1000);
            while (_queue.Count > 0)
            {
                object item;
                while (_queue.TryDequeue(out item))
                    _consumer.Consume(item);
                Thread.Sleep(1000);
            }
        }

        [TearDown]
        public void CleanUp()
        {
            _queue.Clear();
            _queue = null;
            _producer = null;
            _consumer = null;
            _mockRepository = null;
        }
    }

    public interface IProducer
    {
        object ProduceItem();
    }

    public interface IConsumer
    {
        bool Consume(object item);
    }
}
