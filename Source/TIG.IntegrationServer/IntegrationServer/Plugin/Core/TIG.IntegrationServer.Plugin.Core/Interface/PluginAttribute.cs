using System;

namespace TIG.IntegrationServer.Plugin.Core.Interface
{
    [Serializable]
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class PluginAttribute : Attribute
    {
        #region Constructors
        public PluginAttribute(string guid)
        {
            Id = Guid.Parse(guid);
        }

        #endregion


        #region Public Properties

        public Guid Id { get; private set; }

        public string Name { get; set; }

        #endregion
    }
}
