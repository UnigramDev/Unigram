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
        private readonly Dictionary<int, int> _indexToSize = new Dictionary<int, int> { { 0, 12 }, { 1, 13 }, { 2, 14 }, { 3, 15 }, { 4, 16 }, { 5, 17 }, { 6, 18 } };
        private readonly Dictionary<int, int> _sizeToIndex = new Dictionary<int, int> { { 12, 0 }, { 13, 1 }, { 14, 2 }, { 15, 3 }, { 16, 4 }, { 17, 5 }, { 18, 6 } };

        public SettingsAppearanceViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Message1 = new TLMessage
            {
                Message = "Ahh you kids today with techno music! Enjoy the classics, like Hasselhoff!",
                Date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now.AddSeconds(-25)),
                HasReplyToMsgId = true,
                ReplyToMsgId = int.MaxValue,
                Reply = new TLMessage
                {
                    Message = "Reinhardt, we need to find you some new tunes.",
                    From = new TLUser
                    {
                        HasFirstName = true,
                        FirstName = "Lucio"
                    }
                }
            };

            Message2 = new TLMessage
            {
                Message = "I can't take you seriously right now. Sorry..",
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
