using System.Numerics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;


namespace Telegram.Api
{
    public static class TLExtensions
    {
        public static bool CodeEquals(this TLRPCError error, TLErrorCode code)
        {
            if (error == null)
            {
                return false;
            }

            if (Enum.IsDefined(typeof(TLErrorCode), error.ErrorCode))
            {
                return (TLErrorCode)error.ErrorCode == code;
            }

            return false;
        }

        public static bool TypeEquals(this TLRPCError error, TLErrorType type)
        {
            if (error == null || error.ErrorMessage == null)
            {
                return false;
            }

            var strings = error.ErrorMessage.Split(':');
            var typeString = strings[0];
            if (Enum.IsDefined(typeof(TLErrorType), typeString))
            {
                var value = (TLErrorType)Enum.Parse(typeof(TLErrorType), typeString, true);

                return value == type;
            }

            return false;
        }

        public static bool TypeStarsWith(this TLRPCError error, TLErrorType type)
        {
            var strings = error.ErrorMessage.Split(':');
            var typeString = strings[0];

            return typeString.StartsWith(type.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static string GetErrorTypeString(this TLRPCError error)
        {
            var strings = error.ErrorMessage.Split(':');
            return strings[0];
        }
    }
}
