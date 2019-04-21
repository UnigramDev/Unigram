using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Views;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;
using Unigram.Collections;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersTrendingViewModel : TLViewModelBase, IHandle<UpdateTrendingStickerSets>
    {
        public SettingsStickersTrendingViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<StickerSetInfo>();
            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.Send(new GetTrendingStickerSets(), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    BeginOnUIThread(() => Items.ReplaceWith(stickerSets.Sets));
                }
            });

            return Task.CompletedTask;
        }

        public void Handle(UpdateTrendingStickerSets e)
        {
            //ProcessStickerSets();
        }

        public MvxObservableCollection<StickerSetInfo> Items { get; private set; }
    }
}
