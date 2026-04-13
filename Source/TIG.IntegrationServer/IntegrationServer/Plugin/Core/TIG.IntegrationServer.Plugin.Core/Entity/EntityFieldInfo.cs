using System;

namespace TIG.IntegrationServer.Plugin.Core.Entity
{
    public class EntityFieldInfo
    {
        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public EntityFieldInfo() { }

        /// <summary>
        /// Constructor with required name and type
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="type">Field Type</param>
        public EntityFieldInfo(string name, Type type)
        {
            Name = name;
            Type = type;
        }
        
        /// <summary>
        /// Constructor with required name
        /// </summary>
        /// <param name="name">Field name</param>
        public EntityFieldInfo(string name)
        {
            Name = name;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Field Name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Field Type
        /// </summary>
        public Type Type { get; set; }

        #endregion
    }
}