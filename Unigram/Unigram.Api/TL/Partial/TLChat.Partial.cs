using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLChat : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);

            var c = chat as TLChat;
            if (c != null)
            {
                Title = c.Title;
                if (Photo.GetType() != c.Photo.GetType())
                {
                    Photo = c.Photo;    // при удалении фото чата не обновляется UI при _photo = c.Photo
                }
                else
                {
                    Photo.Update(c.Photo);
                }
                ParticipantsCount = c.ParticipantsCount;
                Date = c.Date;
                IsLeft = c.IsLeft;
                Version = c.Version;

                Flags = c.Flags;
                //if (c.CustomFlags != null)
                //{
                //    CustomFlags = c.CustomFlags;
                //}
            }
        }
    }
}
