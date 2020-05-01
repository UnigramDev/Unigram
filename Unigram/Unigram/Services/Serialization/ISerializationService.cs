using System;

namespace Unigram.Services.Serialization
{
    public interface ISerializationService
    {
        /// <summary>
        /// Serializes the parameter.
        /// </summary>
        string Serialize(object parameter);

        /// <summary>
        /// Deserializes the parameter.
        /// </summary>
        object Deserialize(string parameter);

        /// <summary>
        /// Deserializes the parameter.
        /// </summary>
        T Deserialize<T>(string parameter);

        /// <summary>
        /// Attempts to deserialize the parameter.
        /// </summary>
        bool TryDeserialize<T>(string parameter, out T result);
    }
}