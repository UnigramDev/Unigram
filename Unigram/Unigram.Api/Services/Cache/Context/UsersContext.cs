using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Universal.WinSQLite;

namespace Telegram.Api.Services.Cache.Context
{
    public class UsersContext : Context<TLUserBase>
    {
        private readonly string _fields = "`id`,`access_hash`,`flags`,`first_name`,`last_name`,`phone`,`username`,`restriction_reason`,`bot_info_version`,`bot_inline_placeholder`,`photo_id`,`photo_small_local_id`,`photo_small_secret`,`photo_small_volume_id`,`photo_small_dc_id`,`photo_big_local_id`,`photo_big_secret`,`photo_big_volume_id`,`photo_big_dc_id`,`status`,`status_was_online`,`status_expires`";
        private readonly Database _database;

        public UsersContext(Database database)
        {
            _database = database;

            using (Transaction())
            {
                Statement statement;
                Sqlite3.sqlite3_prepare_v2(_database, $"SELECT {_fields} FROM `Users` WHERE `flags` & 2048 != 0", out statement);

                while (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                {
                    var item = GetItemFromStatement(ref statement);
                    if (item != null)
                    {
                        base[item.Id] = item;
                    }
                }

                Sqlite3.sqlite3_finalize(statement);
            }
        }

        public IDisposable Transaction()
        {
            return new DatabaseTransaction(_database);
        }

        public override TLUserBase this[long index]
        {
            get
            {
                if (TryGetValue(index, out TLUserBase value))
                {
                    return value;
                }

                Statement statement;
                Sqlite3.sqlite3_prepare_v2(_database, $"SELECT {_fields} FROM `Users` WHERE `id` = {index}", out statement);

                TLUserBase result = null;
                if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                {
                    base[index] = GetItemFromStatement(ref statement);
                }

                Sqlite3.sqlite3_finalize(statement);
                return result;
            }
            set
            {
                base[index] = value;

                if (value is TLUser user)
                {
                    Statement statement;
                    Sqlite3.sqlite3_prepare_v2(_database, $"INSERT OR REPLACE INTO `Users` ({_fields}) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", out statement);

                    Sqlite3.sqlite3_bind_int64(statement, 1, user.Id);

                    if (user.HasAccessHash)
                    {
                        Sqlite3.sqlite3_bind_int64(statement, 2, user.AccessHash.Value);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 2);
                    }

                    Sqlite3.sqlite3_bind_int(statement, 3, (int)user.Flags);

                    if (user.HasFirstName && !string.IsNullOrEmpty(user.FirstName))
                    {
                        Sqlite3.sqlite3_bind_text(statement, 4, user.FirstName, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 4);
                    }

                    if (user.HasLastName && !string.IsNullOrEmpty(user.LastName))
                    {
                        Sqlite3.sqlite3_bind_text(statement, 5, user.LastName, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 5);
                    }

                    if (user.HasPhone && !string.IsNullOrEmpty(user.Phone))
                    {
                        Sqlite3.sqlite3_bind_text(statement, 6, user.Phone, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 6);
                    }

                    if (user.HasUsername && !string.IsNullOrEmpty(user.Username))
                    {
                        Sqlite3.sqlite3_bind_text(statement, 7, user.Username, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 7);
                    }

                    if (user.HasRestrictionReason && !string.IsNullOrEmpty(user.RestrictionReason))
                    {
                        Sqlite3.sqlite3_bind_text(statement, 8, user.RestrictionReason, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 8);
                    }

                    if (user.HasBotInfoVersion && user.BotInfoVersion.HasValue)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 9, user.BotInfoVersion.Value);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 9);
                    }

                    if (user.HasBotInlinePlaceholder && !string.IsNullOrEmpty(user.BotInlinePlaceholder))
                    {
                        Sqlite3.sqlite3_bind_text(statement, 10, user.BotInlinePlaceholder, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 10);
                    }

                    if (user.HasPhoto && user.Photo is TLUserProfilePhoto photo && photo.PhotoSmall is TLFileLocation small && photo.PhotoBig is TLFileLocation big)
                    {
                        Sqlite3.sqlite3_bind_int64(statement, 11, photo.PhotoId);
                        Sqlite3.sqlite3_bind_int(statement, 12, small.LocalId);
                        Sqlite3.sqlite3_bind_int64(statement, 13, small.Secret);
                        Sqlite3.sqlite3_bind_int64(statement, 14, small.VolumeId);
                        Sqlite3.sqlite3_bind_int(statement, 15, small.DCId);
                        Sqlite3.sqlite3_bind_int(statement, 16, big.LocalId);
                        Sqlite3.sqlite3_bind_int64(statement, 17, big.Secret);
                        Sqlite3.sqlite3_bind_int64(statement, 18, big.VolumeId);
                        Sqlite3.sqlite3_bind_int(statement, 19, big.DCId);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 11);
                        Sqlite3.sqlite3_bind_null(statement, 12);
                        Sqlite3.sqlite3_bind_null(statement, 13);
                        Sqlite3.sqlite3_bind_null(statement, 14);
                        Sqlite3.sqlite3_bind_null(statement, 15);
                        Sqlite3.sqlite3_bind_null(statement, 16);
                        Sqlite3.sqlite3_bind_null(statement, 17);
                        Sqlite3.sqlite3_bind_null(statement, 18);
                        Sqlite3.sqlite3_bind_null(statement, 19);
                    }

                    if (user.HasStatus && user.Status is TLUserStatusOffline offline)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 20, (int)user.Status.TypeId);
                        Sqlite3.sqlite3_bind_int(statement, 21, offline.WasOnline);
                        Sqlite3.sqlite3_bind_null(statement, 22);
                    }
                    else if (user.HasStatus && user.Status is TLUserStatusOnline online)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 20, (int)user.Status.TypeId);
                        Sqlite3.sqlite3_bind_null(statement, 21);
                        Sqlite3.sqlite3_bind_int(statement, 22, online.Expires);
                    }
                    else if (user.HasStatus)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 20, (int)user.Status.TypeId);
                        Sqlite3.sqlite3_bind_null(statement, 21);
                        Sqlite3.sqlite3_bind_null(statement, 22);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 20);
                        Sqlite3.sqlite3_bind_null(statement, 21);
                        Sqlite3.sqlite3_bind_null(statement, 22);
                    }

                    Sqlite3.sqlite3_step(statement);
                    Sqlite3.sqlite3_reset(statement);

                    Sqlite3.sqlite3_finalize(statement);
                }
            }
        }

        private TLUserBase GetItemFromStatement(ref Statement statement)
        {
            var flags = (TLUser.Flag)Sqlite3.sqlite3_column_int(statement, 2);
            var id = Sqlite3.sqlite3_column_int(statement, 0);

            long? access_hash = null;
            if (flags.HasFlag(TLUser.Flag.AccessHash))
            {
                access_hash = Sqlite3.sqlite3_column_int64(statement, 1);
            }

            var first_name = Sqlite3.sqlite3_column_text(statement, 3);
            var last_name = Sqlite3.sqlite3_column_text(statement, 4);
            var phone = Sqlite3.sqlite3_column_text(statement, 5);
            var username = Sqlite3.sqlite3_column_text(statement, 6);
            var restriction_reason = Sqlite3.sqlite3_column_text(statement, 7);

            int? bot_info_version = null;
            if (flags.HasFlag(TLUser.Flag.BotInfoVersion))
            {
                bot_info_version = Sqlite3.sqlite3_column_int(statement, 8);
            }

            var bot_inline_placeholder = Sqlite3.sqlite3_column_text(statement, 9);

            TLUserProfilePhotoBase photo = null;
            if (flags.HasFlag(TLUser.Flag.Photo))
            {
                var photo_id = Sqlite3.sqlite3_column_int64(statement, 10);
                var photo_small_local_id = Sqlite3.sqlite3_column_int(statement, 11);
                var photo_small_secret = Sqlite3.sqlite3_column_int64(statement, 12);
                var photo_small_volume_id = Sqlite3.sqlite3_column_int64(statement, 13);
                var photo_small_dc_id = Sqlite3.sqlite3_column_int(statement, 14);

                var photo_big_local_id = Sqlite3.sqlite3_column_int(statement, 15);
                var photo_big_secret = Sqlite3.sqlite3_column_int64(statement, 16);
                var photo_big_volume_id = Sqlite3.sqlite3_column_int64(statement, 17);
                var photo_big_dc_id = Sqlite3.sqlite3_column_int(statement, 18);

                photo = new TLUserProfilePhoto
                {
                    PhotoId = photo_id,
                    PhotoSmall = new TLFileLocation
                    {
                        LocalId = photo_small_local_id,
                        Secret = photo_small_secret,
                        VolumeId = photo_small_volume_id,
                        DCId = photo_small_dc_id
                    },
                    PhotoBig = new TLFileLocation
                    {
                        LocalId = photo_big_local_id,
                        Secret = photo_big_secret,
                        VolumeId = photo_big_volume_id,
                        DCId = photo_big_dc_id
                    }
                };
            }

            TLUserStatusBase status = null;
            if (flags.HasFlag(TLUser.Flag.Status))
            {
                var status_type = (TLType)Sqlite3.sqlite3_column_int(statement, 19);
                if (status_type == TLType.UserStatusOffline)
                {
                    var status_was_online = Sqlite3.sqlite3_column_int(statement, 20);
                    status = new TLUserStatusOffline { WasOnline = status_was_online };
                }
                else if (status_type == TLType.UserStatusOnline)
                {
                    var status_expires = Sqlite3.sqlite3_column_int(statement, 21);
                    status = new TLUserStatusOnline { Expires = status_expires };
                }
                else
                {
                    status = TLFactory.Read<TLUserStatusBase>(null, status_type);
                }
            }

            return new TLUser
            {
                Id = id,
                AccessHash = access_hash,
                Flags = flags,
                FirstName = first_name,
                LastName = last_name,
                Phone = phone,
                Username = username,
                RestrictionReason = restriction_reason,
                BotInfoVersion = bot_info_version,
                BotInlinePlaceholder = bot_inline_placeholder,
                Photo = photo,
                Status = status
            };
        }
    }
}