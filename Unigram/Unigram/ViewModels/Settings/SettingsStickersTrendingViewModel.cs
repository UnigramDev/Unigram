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
using Unigram.Core.Common;
using Telegram.Td.Api;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersTrendingViewModel : UnigramViewModelBase, IHandle<UpdateTrendingStickerSets>
    {
        public SettingsStickersTrendingViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
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
