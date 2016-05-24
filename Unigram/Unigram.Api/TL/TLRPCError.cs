using System;
#if !WIN_RT
using System.Net.Sockets;
#endif

namespace Telegram.Api.TL
{
    public enum ErrorType
    {
        PHONE_MIGRATE,
        NETWORK_MIGRATE, 
        FILE_MIGRATE,
        USER_MIGRATE,
        PHONE_NUMBER_INVALID,
        PHONE_CODE_EMPTY,
        PHONE_CODE_EXPIRED,
        PHONE_CODE_INVALID,
        PHONE_NUMBER_OCCUPIED,
        PHONE_NUMBER_UNOCCUPIED,
        FLOOD_WAIT,
        PEER_FLOOD,
        FIRSTNAME_INVALID,
        LASTNAME_INVALID,
        QUERY_TOO_SHORT,
        USERNAME_INVALID,
        USERNAME_OCCUPIED,
        USERNAME_NOT_OCCUPIED,  // 400
        USERNAME_NOT_MODIFIED,  // 400
        CHANNELS_ADMIN_PUBLIC_TOO_MUCH, // 400
        CHANNEL_PRIVATE,        // 400
        PEER_ID_INVALID,        // 400    
        MESSAGE_EMPTY,          // 400
        MESSAGE_TOO_LONG,       // 400
        MSG_WAIT_FAILED,        // 400

        PASSWORD_HASH_INVALID,  // 400
        NEW_PASSWORD_BAD,       // 400
        NEW_SALT_INVALID,       // 400
        EMAIL_INVALID,          // 400
        EMAIL_UNCONFIRMED,      // 400

        CODE_EMPTY,             // 400
        CODE_INVALID,           // 400
        PASSWORD_EMPTY,         // 400
        PASSWORD_RECOVERY_NA,   // 400
        PASSWORD_RECOVERY_EXPIRED,  //400

        CHAT_INVALID,           // 400
        CHAT_ADMIN_REQUIRED,    // 400   
        CHAT_NOT_MODIFIED,      // 400
        CHAT_ABOUT_NOT_MODIFIED,// 400
        INVITE_HASH_EMPTY,      // 400
        INVITE_HASH_INVALID,    // 400
        INVITE_HASH_EXPIRED,    // 400
        USERS_TOO_MUCH,         // 400
        USER_ALREADY_PARTICIPANT,   // 400
        USER_NOT_PARTICIPANT,   // 400

        STICKERSET_INVALID,     // 400

        SESSION_PASSWORD_NEEDED,// 401
        SESSION_REVOKED         // 401
        ,
    }

    public enum ErrorCode
    {
        ERROR_SEE_OTHER = 303,
        BAD_REQUEST = 400,
        UNAUTHORIZED = 401,
        FORBIDDEN = 403,
        NOT_FOUND = 404,
        FLOOD = 420,
        INTERNAL = 500,

        #region Additional
        TIMEOUT = 408,
        #endregion
    }

    public class TLRPCReqError : TLRPCError
    {
        public new const uint Signature = TLConstructors.TLRPCReqError;

        public TLLong QueryId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            QueryId = GetObject<TLLong>(bytes, ref position);
            Code = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                QueryId.ToBytes(),
                Code.ToBytes(),
                Message.ToBytes());
        }
    }

    public class TLRPCError : TLObject
    {
        public TLRPCError()
        {
            
        }

        public TLRPCError(int errorCode)
        {
            Code = new TLInt(errorCode);
        }

        #region Additional
#if !WIN_RT
        public SocketError? SocketError { get; set; }
#endif
        
        public Exception Exception { get; set; }

        /// <summary>
        /// Await time before next request (ms)
        /// </summary>
        public int AwaitTime { get; set; }
        #endregion


        public bool CodeEquals(ErrorCode code)
        {
            if (Enum.IsDefined(typeof (ErrorCode), Code.Value))
            {
                return (ErrorCode) Code.Value == code;
            }

            return false;
        }

        public static bool CodeEquals(TLRPCError error, ErrorCode code)
        {
            if (Enum.IsDefined(typeof(ErrorCode), error.Code.Value))
            {
                return (ErrorCode)error.Code.Value == code;
            }

            return false;
        }

        public string GetErrorTypeString()
        {
            var strings = Message.ToString().Split(':');
            return strings[0];
        }

        public bool TypeStarsWith(ErrorType type)
        {
            var strings = Message.ToString().Split(':');
            var typeString = strings[0];

            return typeString.StartsWith(type.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public bool TypeEquals(ErrorType type)
        {
            if (Message == null) return false;

            var strings = Message.ToString().Split(':');
            var typeString = strings[0];
            if (Enum.IsDefined(typeof(ErrorType), typeString))
            {
                var value = (ErrorType)Enum.Parse(typeof (ErrorType), typeString, true);

                return value == type;
            }

            return false;
        }

        public static bool TypeEquals(TLRPCError error, ErrorType type)
        {
            if (error == null) return false;

            return error.TypeEquals(type);
        }

        public const uint Signature = TLConstructors.TLRPCError;

        public TLInt Code { get; set; }

        public TLString Message { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Code = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Code.ToBytes(),
                Message.ToBytes());
        }

        public override string ToString()
        {
#if DEBUG
            return string.Format("{0} {1}{2}{3}", Code, Message, 
#if WINDOWS_PHONE
                SocketError != null ? "\nSocketError=" + SocketError : string.Empty, 
#else
                string.Empty,
#endif
                Exception != null ? "\nException=" : string.Empty);
#else
            return string.Format("{0} {1}", Code, Message);
#endif
        }
    }
}