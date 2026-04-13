using System.Reflection;

namespace TIG.IntegrationServer.DI.Autofac
{
    public static class AssemblyBeacon
    {
        public static Assembly Assembly
        {
            get { return typeof(AssemblyBeacon).Assembly; }
        }
    }
}
