using System;
using System.IO;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.Provider
{
    public class XmlSerializeProvider
    {
        #region Public Methonds

        /// <summary>
        /// Deserialize object from xml text.
        /// </summary>
        /// <typeparam name="T">Type of deserialize object.</typeparam>
        /// <param name="xml">Xml Text.</param>
        /// <returns>Object from xml text</returns>
        public static T Deserialize<T>(string xml)
        {
            try
            {
                using (var sr = new StringReader(xml))
                {
                    var xmldes = new XmlSerializer(typeof(T));
                    return (T)xmldes.Deserialize(sr);
                }
            }
            catch (Exception e)
            {
                return default(T);
            }
        }

        /// <summary>
        /// Deserialize object from xml stream.
        /// </summary>
        /// <typeparam name="T">Type of deserialize object.</typeparam>
        /// <param name="stream">Xml stream</param>
        /// <returns>Object from xml</returns>
        public static T Deserialize<T>(Stream stream)
        {
            var xmldes = new XmlSerializer(typeof(T));
            return (T)xmldes.Deserialize(stream);
        }

        /// <summary>
        /// Serializer object to a xml text.
        /// </summary>
        /// <typeparam name="T">Type of object</typeparam>
        /// <param name="obj">Object be use for serialize</param>
        /// <returns>Xml text</returns>
        public static string Serialize<T>(T obj)
        {
            var stream = new MemoryStream();
            var xml = new XmlSerializer(typeof(T));

            xml.Serialize(stream, obj);

            stream.Position = 0;
            var sr = new StreamReader(stream);
            var str = sr.ReadToEnd();
            return str;
        }

        #endregion
    }
}