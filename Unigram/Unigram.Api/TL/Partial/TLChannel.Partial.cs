using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLChannel : INotifyPropertyChanged
    {
        public Int32 AdminsCount { get; set; }

        public Int32? ParticipantsCount { get; set; }

        public TLVector<int> ParticipantIds { get; set; }

        public Int64? ReadInboxMaxId { get; set; }

        public Int64? ReadOutboxMaxId { get; set; }

        public Int32? PinnedMsgId { get; set; }

        public Int32? HiddenPinnedMsgId { get; set; }

        public Int32? Pts { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
