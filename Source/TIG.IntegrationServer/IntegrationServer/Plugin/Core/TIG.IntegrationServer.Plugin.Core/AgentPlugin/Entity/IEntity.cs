namespace TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity
{
    public interface IEntity
    {
        /// <summary>
        /// Entity unique identifier. Should be set by the agent. Sync engine can only read it.
        /// </summary>
        object Id { get; }

        void SetValue(string propertyName, object value);

        object GetValue(string propertyname);

        string[] EntityPropertyNames();

        bool IsEmpty();
    }
}
