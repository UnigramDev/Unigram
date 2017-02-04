using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Template10.Services.SerializationService;

namespace Unigram.Core.Services
{
    public class TLSerializationService : ISerializationService
    {
        private static TLSerializationService _current;
        public static TLSerializationService Current
        {
            get
            {
                if (_current == null)
                    _current = new TLSerializationService();

                return _current;
            }
        }

        public object Deserialize(string parameter)
        {
            return Deserialize<object>(parameter);
        }

        public bool TryDeserialize<T>(string parameter, out T result)
        {
            try
            {
                result = Deserialize<T>(parameter);
                return true;
            }
            catch { }

            result = default(T);
            return false;
        }

        public T Deserialize<T>(string parameter)
        {
            if (parameter == null)
            {
                return (T)(object)null;
            }

            if (parameter.StartsWith("{"))
            {
                return SerializationService.Json.Deserialize<T>(parameter);
            }

            var length = parameter.Length;
            var bytes = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
                bytes[i / 2] = Convert.ToByte(parameter.Substring(i, 2), 16);

            using (var from = new TLBinaryReader(bytes))
            {
                return TLFactory.Read<T>(from);
            }
        }

        public string Serialize(object parameter)
        {
            if (parameter == null)
            {
                return null;
            }

            if (parameter is TLObject)
            {
                var obj = parameter as TLObject;
                using (var stream = new MemoryStream())
                {
                    using (var to = new TLBinaryWriter(stream))
                    {
                        obj.Write(to);
                    }

                    return BitConverter.ToString(stream.ToArray()).Replace("-", string.Empty);
                }
            }

            return SerializationService.Json.Serialize(parameter);
        }
    }
}
