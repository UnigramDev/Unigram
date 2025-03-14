//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Collections
{
    public interface ISearchChatsFilter
    {
        public string Text { get; }
        public string Glyph { get; }
    }

    public partial class SearchChatsFilterContent : ISearchChatsFilter
    {
        private readonly SearchMessagesFilter _filter;

        public SearchChatsFilterContent(SearchMessagesFilter filter)
        {
            _filter = filter;
        }

        public SearchMessagesFilter Filter => _filter;

        public string Text => _filter switch
        {
            SearchMessagesFilterPhotoAndVideo => Strings.SharedMediaTab2,
            SearchMessagesFilterDocument => Strings.SharedFilesTab2,
            SearchMessagesFilterUrl => Strings.SharedLinksTab2,
            SearchMessagesFilterAudio => Strings.SharedMusicTab2,
            SearchMessagesFilterVoiceNote => Strings.SharedVoiceTab2,
            _ => null,
        };

        public string Glyph => _filter switch
        {
            SearchMessagesFilterPhotoAndVideo => Icons.Image,
            SearchMessagesFilterDocument => Icons.Document,
            SearchMessagesFilterUrl => Icons.Link,
            SearchMessagesFilterAudio => Icons.MusicNote,
            SearchMessagesFilterVoiceNote => Icons.MicOn,
            _ => null,
        };
    }

    public partial class SearchChatsFilterChat : ISearchChatsFilter
    {
        private readonly IClientService _clientService;
        private readonly Chat _chat;

        public SearchChatsFilterChat(IClientService clientService, Chat chat)
        {
            _clientService = clientService;
            _chat = chat;
        }

        public long Id => _chat.Id;

        public string Text => _clientService.GetTitle(_chat);

        public string Glyph => _chat.Type switch
        {
            ChatTypePrivate => Icons.Person,
            ChatTypeBasicGroup => Icons.People,
            ChatTypeSupergroup supergroup => supergroup.IsChannel ? Icons.People : Icons.Megaphone,
            _ => null,
        };
    }

    //public partial class SearchChatsFilterDateRange : ISearchChatsFilter
    //{
    //    private readonly DateRange _range;

    //    public SearchChatsFilterDateRange(DateRange range)
    //    {
    //        _range = range;
    //    }

    //    public int EndDate => _range.EndDate;
    //    public int StartDate => _range.StartDate;

    //    public string Text
    //    {
    //        get
    //        {
    //            var start = Formatter.ToLocalTime(_range.StartDate);
    //            var end = Formatter.ToLocalTime(_range.EndDate);

    //            if (start.DayOfYear == end.DayOfYear)
    //            {
    //                return Formatter.DayMonthFullYear.Format(start);
    //            }
    //            else if (start.Month == end.Month)
    //            {
    //                return Formatter.MonthAbbreviatedYear.Format(start);
    //            }

    //            return start.Year.ToString();
    //        }
    //    }

    //    public string Glyph => Icons.Calendar;
    //}
}
