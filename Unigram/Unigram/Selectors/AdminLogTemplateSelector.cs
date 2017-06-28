using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class AdminLogTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ChangeTitle { get; set; }
        public DataTemplate ChangeAbout { get; set; }
        public DataTemplate ChangeUsername { get; set; }
        public DataTemplate ChangePhoto { get; set; }
        public DataTemplate ToggleInvites { get; set; }
        public DataTemplate ToggleSignatures { get; set; }
        public DataTemplate UpdatePinned { get; set; }
        public DataTemplate EditMessage { get; set; }
        public DataTemplate DeleteMessage { get; set; }
        public DataTemplate ParticipantJoin { get; set; }
        public DataTemplate ParticipantLeave { get; set; }
        public DataTemplate ParticipantInvite { get; set; }
        public DataTemplate ParticipantToggleBan { get; set; }
        public DataTemplate ParticipantToggleAdmin { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is AdminLogEvent log)
            {
                item = log.Event;
            }

            var adminEvent = item as TLChannelAdminLogEvent;
            if (adminEvent != null)
            {
                switch (adminEvent.Action)
                {
                    case TLChannelAdminLogEventActionChangeTitle changeTitle:
                        return ChangeTitle;
                    case TLChannelAdminLogEventActionChangeAbout changeAbout:
                        return ChangeAbout;
                    case TLChannelAdminLogEventActionChangeUsername changeUsername:
                        return ChangeUsername;
                    case TLChannelAdminLogEventActionChangePhoto changePhoto:
                        return ChangePhoto;
                    case TLChannelAdminLogEventActionToggleInvites toggleInvites:
                        return ToggleInvites;
                    case TLChannelAdminLogEventActionToggleSignatures toggleSignatures:
                        return ToggleSignatures;
                    case TLChannelAdminLogEventActionUpdatePinned updatePinned:
                        return UpdatePinned;
                    case TLChannelAdminLogEventActionEditMessage editMessage:
                        return EditMessage;
                    case TLChannelAdminLogEventActionDeleteMessage deleteMessage:
                        return DeleteMessage;
                    case TLChannelAdminLogEventActionParticipantJoin participantJoin:
                        return ParticipantJoin;
                    case TLChannelAdminLogEventActionParticipantInvite participantInvite:
                        return ParticipantInvite;
                    case TLChannelAdminLogEventActionParticipantToggleBan participantToggleBan:
                        return ParticipantToggleBan;
                    case TLChannelAdminLogEventActionParticipantToggleAdmin participantToggleAdmin:
                        return ParticipantToggleAdmin;
                }
            }

            return base.SelectTemplateCore(item);
        }
    }
}
