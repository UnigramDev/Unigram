using System;

namespace Unigram.Services.Serialization
{
    public static class SerializationService
    {
        private static ISerializationService _json;
        public static ISerializationService Json => _json ?? (_json = new JsonSerializationService());
    }
}