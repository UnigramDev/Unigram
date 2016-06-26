using Telegram.Api.TL;

namespace Telegram.Api.Services
{
    public class MTProtoResponse
    {
        internal object _value;
        internal TLRPCError _error;

        public MTProtoResponse(object value)
        {
            _value = value;
        }

        public MTProtoResponse(TLRPCError error)
        {
            _error = error;
        }

        public MTProtoResponse(object value, TLRPCError error)
        {
            _value = value;
            _error = error;
        }
    }

    public class MTProtoResponse<T>
    {
        public MTProtoResponse(object value)
        {
            Value = (T)value;
        }

        public MTProtoResponse(TLRPCError error)
        {
            Error = error;
        }

        public MTProtoResponse(object value, TLRPCError error)
        {
            Value = (T)value;
            Error = error;
        }

        public T Value { get; protected set; }

        public TLRPCError Error { get; set; }

        public bool IsSucceeded
        {
            get
            {
                return Error == null && !Value.Equals(default(T));
            }
        }

        public static implicit operator MTProtoResponse<T>(MTProtoResponse str)
        {
            return new MTProtoResponse<T>(str._value, str._error);
        }

        public static implicit operator MTProtoResponse(MTProtoResponse<T> str)
        {
            return new MTProtoResponse(str.Value, str.Error);
        }
    }
}
