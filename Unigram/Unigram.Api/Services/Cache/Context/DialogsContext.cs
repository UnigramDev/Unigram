using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Universal.WinSQLite;

namespace Telegram.Api.Services.Cache.Context
{
    public class DialogsContext : Context<TLDialog>
    {
        private const ulong PeerIdMask = 0xFFFFFFFFUL;
        private const ulong PeerIdTypeMask = 0x300000000UL;
        private const ulong PeerIdUserShift = 0x000000000UL;
        private const ulong PeerIdChatShift = 0x100000000UL;
        private const ulong PeerIdChannelShift = 0x200000000UL;

        private readonly string _fields = "`flags`,`peer`,`index`,`top_message`,`read_inbox_max_id`,`read_outbox_max_id`,`unread_count`,`notify_settings_flags`,`notify_settings_mute_until`,`notify_settings_sound`,`pts`,`draft_date`,`draft_entities`,`draft_flags`,`draft_message`,`draft_reply_to_msg_id`";
        private readonly Database _database;

        private readonly JsonSerializerSettings _settings;

        public DialogsContext(Database database)
        {
            _database = database;
            _settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

            using (Transaction())
            {
                Statement statement;
                Sqlite3.sqlite3_prepare_v2(_database, $"SELECT {_fields} FROM `Dialogs`", out statement);

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

        public override TLDialog this[long index]
        {
            get
            {
                if (TryGetValue(index, out TLDialog value))
                {
                    return value;
                }

                Statement statement;
                Sqlite3.sqlite3_prepare_v2(_database, $"SELECT {_fields} FROM `Dialogs` WHERE `index` = {index}", out statement);

                TLDialog result = null;
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

                if (value is TLDialog dialog)
                {
                    Statement statement;
                    Sqlite3.sqlite3_prepare_v2(_database, $"INSERT OR REPLACE INTO `Dialogs` ({_fields}) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", out statement);

                    Sqlite3.sqlite3_bind_int(statement, 1, (int)dialog.Flags);

                    if (dialog.Peer is TLPeerUser peerUser)
                    {
                        Sqlite3.sqlite3_bind_int64(statement, 2, (long)(PeerIdUserShift | (ulong)(uint)peerUser.UserId));
                    }
                    else if (dialog.Peer is TLPeerChat peerChat)
                    {
                        Sqlite3.sqlite3_bind_int64(statement, 2, (long)(PeerIdChatShift | (ulong)(uint)peerChat.ChatId));
                    }
                    if (dialog.Peer is TLPeerChannel peerChannel)
                    {
                        Sqlite3.sqlite3_bind_int64(statement, 2, (long)(PeerIdChannelShift | (ulong)(uint)peerChannel.ChannelId));
                    }

                    Sqlite3.sqlite3_bind_int(statement, 3, dialog.Id);
                    Sqlite3.sqlite3_bind_int(statement, 4, dialog.TopMessage);
                    Sqlite3.sqlite3_bind_int(statement, 5, dialog.ReadInboxMaxId);
                    Sqlite3.sqlite3_bind_int(statement, 6, dialog.ReadOutboxMaxId);
                    Sqlite3.sqlite3_bind_int(statement, 7, dialog.UnreadCount);

                    if (dialog.NotifySettings is TLPeerNotifySettings notifySettings)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 8, (int)notifySettings.Flags);
                        Sqlite3.sqlite3_bind_int(statement, 9, notifySettings.MuteUntil);
                        Sqlite3.sqlite3_bind_text(statement, 10, notifySettings.Sound, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 8);
                        Sqlite3.sqlite3_bind_null(statement, 9);
                        Sqlite3.sqlite3_bind_null(statement, 10);
                    }

                    if (dialog.HasPts && dialog.Pts.HasValue)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 11, dialog.Pts.Value);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 11);
                    }

                    if (dialog.HasDraft && dialog.Draft is TLDraftMessage draft)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 12, (int)dialog.Flags);

                        if (draft.HasReplyToMsgId && draft.ReplyToMsgId.HasValue)
                        {
                            Sqlite3.sqlite3_bind_int(statement, 13, draft.ReplyToMsgId.Value);
                        }
                        else
                        {
                            Sqlite3.sqlite3_bind_null(statement, 13);
                        }

                        Sqlite3.sqlite3_bind_text(statement, 14, draft.Message, -1);

                        if (draft.HasEntities && draft.Entities != null)
                        {
                            Sqlite3.sqlite3_bind_text(statement, 15, JsonConvert.SerializeObject(draft.Entities, _settings), -1);
                        }
                        else
                        {
                            Sqlite3.sqlite3_bind_null(statement, 15);
                        }

                        Sqlite3.sqlite3_bind_int(statement, 16, draft.Date);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 12);
                        Sqlite3.sqlite3_bind_null(statement, 13);
                        Sqlite3.sqlite3_bind_null(statement, 14);
                        Sqlite3.sqlite3_bind_null(statement, 15);
                        Sqlite3.sqlite3_bind_null(statement, 16);
                    }

                    Sqlite3.sqlite3_step(statement);
                    Sqlite3.sqlite3_reset(statement);

                    Sqlite3.sqlite3_finalize(statement);
                }
            }
        }

        private TLDialog GetItemFromStatement(ref Statement statement)
        {
            var flags = (TLDialog.Flag)Sqlite3.sqlite3_column_int(statement, 0);
            var id = Sqlite3.sqlite3_column_int64(statement, 1);

            TLPeerBase peer = null;
            if (((ulong)id & PeerIdTypeMask) == PeerIdUserShift)
            {
                peer = new TLPeerUser { UserId = (int)(uint)((ulong)id & PeerIdMask) };
            }
            else if (((ulong)id & PeerIdTypeMask) == PeerIdChatShift)
            {
                peer = new TLPeerChat { ChatId = (int)(uint)((ulong)id & PeerIdMask) };
            }
            else if (((ulong)id & PeerIdTypeMask) == PeerIdChannelShift)
            {
                peer = new TLPeerChannel { ChannelId = (int)(uint)((ulong)id & PeerIdMask) };
            }

            var top_message = Sqlite3.sqlite3_column_int(statement, 3);
            var read_inbox_max_id = Sqlite3.sqlite3_column_int(statement, 4);
            var read_outbox_max_id = Sqlite3.sqlite3_column_int(statement, 5);
            var unread_count = Sqlite3.sqlite3_column_int(statement, 6);

            TLPeerNotifySettingsBase notifySettings;

            var notifyType = Sqlite3.sqlite3_column_type(statement, 7);
            if (notifyType == 1)
            {
                notifySettings = new TLPeerNotifySettings
                {
                    Flags = (TLPeerNotifySettings.Flag)Sqlite3.sqlite3_column_int(statement, 7),
                    MuteUntil = Sqlite3.sqlite3_column_int(statement, 8),
                    Sound = Sqlite3.sqlite3_column_text(statement, 9)
                };
            }
            else
            {
                notifySettings = new TLPeerNotifySettingsEmpty();
            }

            int? pts = null;
            if (flags.HasFlag(TLDialog.Flag.Pts))
            {
                pts = Sqlite3.sqlite3_column_int(statement, 10);
            }

            TLDraftMessageBase draft = null;
            if (flags.HasFlag(TLDialog.Flag.Draft))
            {
                var draftFlags = (TLDraftMessage.Flag)Sqlite3.sqlite3_column_int(statement, 11);

                int? replyToMsgId = null;
                if (draftFlags.HasFlag(TLDraftMessage.Flag.ReplyToMsgId))
                {
                    replyToMsgId = Sqlite3.sqlite3_column_int(statement, 12);
                }

                var message = Sqlite3.sqlite3_column_text(statement, 13);

                TLVector<TLMessageEntityBase> entities = null;
                if (draftFlags.HasFlag(TLDraftMessage.Flag.Entities))
                {
                    entities = JsonConvert.DeserializeObject<TLVector<TLMessageEntityBase>>(Sqlite3.sqlite3_column_text(statement, 14), _settings);
                }

                draft = new TLDraftMessage
                {
                    Flags = draftFlags,
                    ReplyToMsgId = replyToMsgId,
                    Message = message,
                    Entities = entities,
                    Date = Sqlite3.sqlite3_column_int(statement, 15)
                };
            }

            return new TLDialog
            {
                Flags = flags,
                Peer = peer,
                TopMessage = top_message,
                ReadInboxMaxId = read_inbox_max_id,
                ReadOutboxMaxId = read_outbox_max_id,
                UnreadCount = unread_count,
                NotifySettings = notifySettings,
                Pts = pts,
                Draft = draft
            };
        }
    }
}
