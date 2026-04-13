using System.Configuration;

namespace TIG.IntegrationServer.Common.Configuration
{
    public static class ConfigurationHelper
    {
        /// <summary>
        /// Get private configuration by configration location
        /// </summary>
        /// <typeparam name="T">Return configruation type</typeparam>
        /// <param name="configurationLocation">Where is configuration file</param>
        /// <param name="sectionName">Section name in configuration file</param>
        /// <returns>Configuration section</returns>
        public static T GetPrivateConfiguration<T>(string configurationLocation, string sectionName) where T : ConfigurationSection
        {
            var configuration = ConfigurationManager.OpenExeConfiguration(configurationLocation);
            return configuration.GetSection(sectionName) as T;
        }
    }
}