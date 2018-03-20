using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

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
                var messages = new List<MessageViewModel>(_panel.LastVisibleIndex - _panel.FirstVisibleIndex);
                var auto = ViewModel.Settings.IsAutoPlayEnabled;
                var news = new Dictionary<string, MediaPlayerItem>();

                for (int i = index0; i <= index1; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    if (container != null)
                    {
                        var message = Messages.ItemFromContainer(container) as MessageViewModel;
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

        public void Play(MessageViewModel message)
        {
        }

        public void Play(IEnumerable<MessageViewModel> items, bool auto)
        {
        }
    }
}
