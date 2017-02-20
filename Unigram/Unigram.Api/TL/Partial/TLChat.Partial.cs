using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLChat : ITLReadMaxId, ITLInputPeer, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }

        public override void Update(TLChatBase chatBase)
        {
            base.Update(chatBase);

            var chat = chatBase as TLChat;
            if (chat != null)
            {
                Title = chat.Title;
                if (Photo.GetType() != chat.Photo.GetType())
                {
                    Photo = chat.Photo;    // при удалении фото чата не обновляется UI при _photo = c.Photo
                }
                else
                {
                    Photo.Update(chat.Photo);
                }
                ParticipantsCount = chat.ParticipantsCount;
                Date = chat.Date;
                IsLeft = chat.IsLeft;
                Version = chat.Version;

                Flags = chat.Flags;

                if (chat.ReadInboxMaxId != 0 && (ReadInboxMaxId == 0 || ReadInboxMaxId < chat.ReadInboxMaxId))
                {
                    ReadInboxMaxId = chat.ReadInboxMaxId;
                }
                if (chat.ReadOutboxMaxId != 0 && (ReadOutboxMaxId == 0 || ReadOutboxMaxId < chat.ReadOutboxMaxId))
                {
                    ReadOutboxMaxId = chat.ReadOutboxMaxId;
                }

                //if (c.CustomFlags != null)
                //{
                //    CustomFlags = c.CustomFlags;
                //}
            }
        }

        public int ReadInboxMaxId { get; set; }

        public int ReadOutboxMaxId { get; set; }

        //public override void ReadFromCache(TLBinaryReader from)
        //{
        //    ReadInboxMaxId = from.ReadInt32();
        //    ReadOutboxMaxId = from.ReadInt32();
        //}

        //public override void WriteToCache(TLBinaryWriter to)
        //{
        //    to.Write(ReadInboxMaxId);
        //    to.Write(ReadOutboxMaxId);
        //}

        public TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChat { ChatId = Id };
        }
    }
}
