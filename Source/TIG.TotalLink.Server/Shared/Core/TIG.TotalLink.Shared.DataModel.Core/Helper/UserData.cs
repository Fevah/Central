using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    /// <summary>
    /// Represents the UserData section of authentication tokens.
    /// </summary>
    public class UserData
    {
        #region Constructors

        public UserData()
        {
        }

        public UserData(Guid oid, string displayName)
        {
            Oid = oid;
            DisplayName = displayName;
        }

        public UserData(string userDataString)
        {
            Deserialize(userDataString);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The Oid of the user.
        /// </summary>
        public Guid Oid { get; set; }

        /// <summary>
        /// The DisplayName for the user.
        /// </summary>
        public string DisplayName { get; set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Serializes this UserData to a string.
        /// </summary>
        /// <returns>This UserData as a string.</returns>
        private string Serialize()
        {
            return string.Format("{0}|{1}", Oid.ToString("N"), DisplayName);
        }

        /// <summary>
        /// Deserializes this UserData from a string.
        /// </summary>
        /// <param name="userDataString">The string containing the UserData.</param>
        private void Deserialize(string userDataString)
        {
            // Split the data
            var userDataParts = userDataString.Split('|');

            // Abort if there are not enough values
            if (userDataParts.Length != 2)
                throw new Exception("UserData is invalid.");

            // Extract the Oid
            Guid oid;
            if (!Guid.TryParseExact(userDataParts[0], "N", out oid))
                throw new Exception("UserData.Oid is invalid.");
            Oid = oid;

            // Extract the DisplayName
            DisplayName = userDataParts[1];
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return Serialize();
        }

        #endregion
    }
}
