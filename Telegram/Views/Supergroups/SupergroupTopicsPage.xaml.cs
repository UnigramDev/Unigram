//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupTopicsPage : HostedPage
    {
        public SupergroupTopicsViewModel ViewModel => DataContext as SupergroupTopicsViewModel;

        public SupergroupTopicsPage()
        {
            InitializeComponent();
            Title = Strings.TopicsTitle;

            AnimatedImage.SetAttachedSource(TabsButton, new LocalFileSource("ms-appx:///Assets/Animations/SupergroupTopicsTabs.tgs")
            {
                NeedsRepainting = true
            });

            AnimatedImage.SetAttachedSource(ListButton, new LocalFileSource("ms-appx:///Assets/Animations/SupergroupTopicsList.tgs")
            {
                NeedsRepainting = true
            });

            AnimatedImage.SetAttachedSource(SidebarButton, new LocalFileSource("ms-appx:///Assets/Animations/SupergroupTopicsSidebar.tgs")
            {
                NeedsRepainting = true
            });
        }

        private void Tabs_Checked(object sender, RoutedEventArgs e)
        {
            Play(TabsButton);
        }

        private void List_Checked(object sender, RoutedEventArgs e)
        {
            Play(ListButton);
        }

        private void Sidebar_Checked(object sender, RoutedEventArgs e)
        {
            Play(SidebarButton);
        }

        private void Play(RadioButton button)
        {
            var animated = button.GetChild<AnimatedImage>();
            animated?.Play();
        }
    }
}
