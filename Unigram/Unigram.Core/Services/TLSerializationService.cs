using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Native.TL;
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
            if (string.IsNullOrEmpty(parameter))
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

            if (parameter.StartsWith("0EFFFFFF"))
            {
                return (T)(object)bytes.Skip(4).ToArray();
            }

            var buffer = bytes.AsBuffer();
            var from = TLObjectSerializer.Deserialize(buffer);

            return (T)(object)from;
        }

        public string Serialize(object parameter)
        {
            if (parameter == null)
            {
                return null;
            }

            if (parameter is TLObject obj)
            {
                var buffer = TLObjectSerializer.Serialize(obj);
                var array = buffer.ToArray();

                return BitConverter.ToString(array).Replace("-", string.Empty);
            }

            return SerializationService.Json.Serialize(parameter);
        }
    }
}
