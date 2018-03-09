using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Template10.Services.SerializationService;
using System.Reflection;
using Telegram.Td.Api;
using Newtonsoft.Json.Linq;

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
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                settings.Converters.Add(new TdConverter());

                var container = JsonConvert.DeserializeObject<Container>(parameter);
                var type = Type.GetType(container.Type);

                return (T)JsonConvert.DeserializeObject(container.Data, type, settings);
                //return SerializationService.Json.Deserialize<T>(parameter);
            }

            return default(T);

            //var length = parameter.Length;
            //var bytes = new byte[length / 2];
            //for (int i = 0; i < length; i += 2)
            //    bytes[i / 2] = Convert.ToByte(parameter.Substring(i, 2), 16);

            //if (parameter.StartsWith("0EFFFFFF"))
            //{
            //    return (T)(object)bytes.Skip(4).ToArray();
            //}

            //var buffer = bytes.AsBuffer();
            //var from = TLObjectSerializer.Deserialize(buffer);

            //return (T)(object)from;
        }

        public string Serialize(object parameter)
        {
            if (parameter == null)
            {
                return null;
            }

            //if (parameter is ITLObject obj)
            //{
            //    var buffer = TLObjectSerializer.Serialize(obj);
            //    var array = buffer.ToArray();

            //    return BitConverter.ToString(array).Replace("-", string.Empty);
            //}

            var container = new Container
            {
                Type = parameter.GetType().AssemblyQualifiedName,
                Data = JsonConvert.SerializeObject(parameter, Formatting.None, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All, TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full })
            };

            return JsonConvert.SerializeObject(container);
            //return SerializationService.Json.Serialize(parameter);
        }

        sealed class Container
        {
            public string Type { get; set; }
            public string Data { get; set; }
        }
    }

    class TdConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.Namespace.StartsWith("Telegram.Td.Api");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                //var read = reader.Read();
                //var type = reader.ReadAsString();

                var jobj = JObject.Load(reader);
                var type = jobj["$type"].Value<string>();

                var obj = Activator.CreateInstance(Type.GetType(type));
                serializer.Populate(jobj.CreateReader(), obj);

                return obj;
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
    
    public class TdBundle : List<TdItem>
    {
        public void Add(string key, object value)
        {
            Add(new TdItem { Key = key, Value = value });
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            var result = this.FirstOrDefault(x => x.Key == key);
            if (result != null)
            {
                value = (T)result.Value;
                return true;
            }

            value = default(T);
            return false;
        }

        public static TdBundle Deserialize(string data)
        {
            return JsonConvert.DeserializeObject<TdBundle>(data, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        }


    }

    public class TdItem
    {
        public string Key { get; set; }
        public object Value { get; set; }
    }

    public class ChatMemberNavigation
    {
        public long ChatId { get; set; }
        public int UserId { get; set; }

        public ChatMemberNavigation(long chatId, int userId)
        {
            ChatId = chatId;
            UserId = userId;
        }
    }
}
