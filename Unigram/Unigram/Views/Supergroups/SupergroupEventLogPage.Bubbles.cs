using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Supergroups
{
    public partial class SupergroupEventLogPage : Page, IGifPlayback
    {
        private ItemsStackPanel _panel;
        private Dictionary<string, MediaPlayerItem> _old = new Dictionary<string, MediaPlayerItem>();

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var index0 = _panel.FirstVisibleIndex;
            var index1 = _panel.LastVisibleIndex;

            if (_panel.FirstVisibleIndex > -1 && _panel.LastVisibleIndex > -1 && !e.IsIntermediate)
            {
                var messages = new List<TLMessage>(_panel.LastVisibleIndex - _panel.FirstVisibleIndex);
                var auto = ApplicationSettings.Current.IsAutoPlayEnabled;
                var news = new Dictionary<string, MediaPlayerItem>();

                for (int i = index0; i <= index1; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as ListViewItem;
                    if (container != null)
                    {
                        var message = Messages.ItemFromContainer(container) as TLMessage;
                        if (message == null)
                        {
                            continue;
                        }

                        messages.Add(message);
                    }
                }

                Play(messages, auto);
            }
        }

        class MediaPlayerItem
        {
            public Grid Container { get; set; }
            public MediaPlayerView Presenter { get; set; }
            public bool Watermark { get; set; }
        }

        public void Play(TLMessage message)
        {
        }

        public void Play(IEnumerable<TLMessage> items, bool auto)
        {
        }
    }
}
