using System;

using Telegram.Api.TL;

namespace Telegram.Api.Services
{
    public class MTProtoResponse<T>
    {
        public MTProtoResponse(object value)
        {
        }

        public MTProtoResponse(TLRPCError error)
        {
        }

        public MTProtoResponse(object value, TLRPCError error)
        {
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

                if (Result == null)
                {
                    return Error == null;
                }

                return Error == null && !Result.Equals(default(T));
            }
        }
    }
}
