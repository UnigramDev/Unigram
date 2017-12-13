using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml;

namespace Unigram.ViewModels.Settings
{
    public class SettingsAppearanceViewModel : UnigramViewModelBase
    {
        private readonly Dictionary<int, int> _indexToSize = new Dictionary<int, int> { { 0, 10 }, { 1, 12 }, { 2, 15 }, { 3, 18 }, { 4, 20 } };
        private readonly Dictionary<int, int> _sizeToIndex = new Dictionary<int, int> { { 10, 0 }, { 12, 1 }, { 15, 2 }, { 18, 3 }, { 20, 4 } };

        public SettingsAppearanceViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Message1 = new TLMessage
            {
                Message = "Yes, deeply personal. I hate you. Every little bit of you. Now get out!",
                Date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now.AddSeconds(-25)),
                HasEntities = true,
                Entities = new TLVector<TLMessageEntityBase>
                {
                    new TLMessageEntityItalic { Offset = 63, Length = 8 }
                },
                HasReplyToMsgId = true,
                ReplyToMsgId = int.MaxValue,
                Reply = new TLMessage
                {
                    Message = "There is something... personal in this...",
                    From = new TLUser
                    {
                        HasFirstName = true,
                        FirstName = "Clouseau"
                    }
                }
            };

            Message2 = new TLMessage
            {
                Message = "You want me to leave?",
                Date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now),
                IsFirst = true,
                IsOut = true,
                State = TLMessageState.Read
            };
        }

        public TLMessage Message1 { get; private set; }
        public TLMessage Message2 { get; private set; }

        public double FontSize
        {
            get
            {
                var size = (int)Theme.Current.GetValueOrDefault("MessageFontSize", 15d);
                if (_sizeToIndex.TryGetValue(size, out int index))
                {
                    return (double)index;
                }

                return 2d;
            }
            set
            {
                var index = (int)Math.Round(value);
                if (_indexToSize.TryGetValue(index, out int size))
                {
                    Theme.Current.AddOrUpdateValue("MessageFontSize", (double)size);
                }

                RaisePropertyChanged();
            }
        }

        public int RequestedTheme
        {
            get
            {
                return (int)ApplicationSettings.Current.RequestedTheme;
            }
            set
            {
                ApplicationSettings.Current.RequestedTheme = (ElementTheme)value;
                RaisePropertyChanged();
                RaisePropertyChanged(() => IsThemeChanged);
            }
        }

        public bool IsThemeChanged
        {
            get
            {
                return ApplicationSettings.Current.CurrentTheme != ApplicationSettings.Current.RequestedTheme;
            }
        }
    }
}
