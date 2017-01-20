using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services
{
    public class MTProtoResponse
    {
        internal object _result;
        internal TLRPCError _error;

        public MTProtoResponse(object value)
        {
            _result = value;
        }

        public MTProtoResponse(TLRPCError error)
        {
            _error = error;
        }

        public MTProtoResponse(object value, TLRPCError error)
        {
            _result = value;
            _error = error;
        }
    }

    public class MTProtoResponse<T>
    {
        public MTProtoResponse(object value)
        {
            if (value is TLVectorEmpty)
            {
                value = Activator.CreateInstance<T>();
            }

            Result = (T)value;
        }

        public MTProtoResponse(TLRPCError error)
        {
            Error = error;
        }

        public MTProtoResponse(object value, TLRPCError error)
        {
            if (value is TLVectorEmpty)
            {
                value = Activator.CreateInstance<T>();
            }

            Result = (T)value;
            Error = error;
        }

        public T Result { get; protected set; }

        public TLRPCError Error { get; set; }

        public bool IsSucceeded
        {
            get
            {
                // mtproto doesn't supports void return type, so boolean is used instead.
                // sometimes it can be false, but the request is succeeded anyway.
                if (Result is bool)
                {
                    return Error == null;
                }

                return Error == null && !Result.Equals(default(T));
            }
        }

        public static implicit operator MTProtoResponse<T>(MTProtoResponse str)
        {
            return new MTProtoResponse<T>(str._result, str._error);
        }

        public static implicit operator MTProtoResponse(MTProtoResponse<T> str)
        {
            return new MTProtoResponse(str.Result, str.Error);
        }
    }
}
