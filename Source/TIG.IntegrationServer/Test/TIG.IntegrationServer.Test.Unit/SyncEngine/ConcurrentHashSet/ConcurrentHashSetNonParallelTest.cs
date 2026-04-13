using System;
using NUnit.Framework;
using TIG.IntegrationServer.Common.ConcurrentCollection;

namespace TIG.IntegrationServer.Test.Unit.SyncEngine.ConcurrentHashSet
{
    [TestFixture]
    public class ConcurrentHashSetNonParallelTests
    {
        [Test]
        public void Test1()
        {
            var sut = new ConcurrentHashSet<Guid>();

            var x = Guid.NewGuid();

            sut.Add(x);
            sut.Add(x);

            sut.Remove(x);
            sut.Remove(x);
        }
    }
}
