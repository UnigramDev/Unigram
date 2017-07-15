using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public enum TLErrorType
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
        PHONE_NUMBER_FLOOD,
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
        MESSAGE_ID_INVALID,     // 400
        MESSAGE_NOT_MODIFIED,   // 400
        MESSAGE_EDIT_TIME_EXPIRED, // 400

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
        BOTS_TOO_MUCH,          // 400
        ADMINS_TOO_MUCH,        // 400
        USER_NOT_MUTUAL_CONTACT,    // 400
        USER_ALREADY_PARTICIPANT,   // 400
        USER_NOT_PARTICIPANT,   // 400

        STICKERSET_INVALID,     // 400
        LOCATION_INVALID,       // 400 upload.getFile
        VOLUME_LOC_NOT_FOUND,   // 400 upload.getFile

        SESSION_PASSWORD_NEEDED,// 401
        SESSION_REVOKED,        // 401
        USER_PRIVACY_RESTRICTED,// 403

        //2FA_RECENT_CONFIRM,   // 420
        //2FA_CONFIRM_WAIT_XXX, // 420

        RPC_CALL_FAIL           // 500
    }
}
