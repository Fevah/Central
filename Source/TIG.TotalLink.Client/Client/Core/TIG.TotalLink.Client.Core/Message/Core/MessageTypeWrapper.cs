using System;
using System.ComponentModel;
using TIG.TotalLink.Client.Core.Extension;

namespace TIG.TotalLink.Client.Core.Message.Core
{
    /// <summary>
    /// A wrapper for message types that will display nicely in a listbox.
    /// </summary>
    public class MessageTypeWrapper : IEquatable<MessageTypeWrapper>
    {
        #region Constructors

        public MessageTypeWrapper(Type messageType)
        {
            MessageType = messageType;

            // Calculate the display name for the message type
            var displayName = messageType.Name.AddSpaces();
            var displayNameAttributes = messageType.GetCustomAttributes(typeof(DisplayNameAttribute), false);
            if (displayNameAttributes.Length > 0)
                displayName = ((DisplayNameAttribute)displayNameAttributes[0]).DisplayName;
            DisplayName = displayName;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type of message that this wrapper contains.
        /// </summary>
        public Type MessageType { get; private set; }

        /// <summary>
        /// The display name for the contained message type.
        /// </summary>
        public string DisplayName { get; private set; }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return DisplayName;
        }

        public override int GetHashCode()
        {
            return MessageType.GetHashCode();
        }

        public override bool Equals(object other)
        {
            var wrapper = other as MessageTypeWrapper;
            if (wrapper != null)
                return Equals(wrapper);

            return false;
        }

        #endregion


        #region IEquatable<MessageTypeWrapper>

        public bool Equals(MessageTypeWrapper other)
        {
            if (other == null)
                return false;

            return (MessageType == other.MessageType);
        }

        #endregion
    }
}
