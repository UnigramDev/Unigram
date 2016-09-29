﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLChannel : ITLReadMaxId, INotifyPropertyChanged
    {
        public Int32 AdminsCount { get; set; }

        public Int32? ParticipantsCount { get; set; }

        public TLVector<int> ParticipantIds { get; set; }

        public Int32? ReadInboxMaxId { get; set; }

        public Int32? ReadOutboxMaxId { get; set; }

        public Int32? PinnedMsgId { get; set; }

        public Int32? HiddenPinnedMsgId { get; set; }

        public Int32? Pts { get; set; }

        int ITLReadMaxId.ReadInboxMaxId
        {
            get
            {
                return ReadInboxMaxId ?? 0;
            }
            set
            {
                ReadInboxMaxId = value;
            }
        }

        int ITLReadMaxId.ReadOutboxMaxId
        {
            get
            {
                return ReadOutboxMaxId ?? 0;
            }
            set
            {
                ReadOutboxMaxId = value;
            }
        }

        public override void ReadFromCache(TLBinaryReader from)
        {
            ((ITLReadMaxId)this).ReadInboxMaxId = from.ReadInt32();
            ((ITLReadMaxId)this).ReadOutboxMaxId = from.ReadInt32();
        }

        public override void WriteToCache(TLBinaryWriter to)
        {
            to.Write(((ITLReadMaxId)this).ReadInboxMaxId);
            to.Write(((ITLReadMaxId)this).ReadOutboxMaxId);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
