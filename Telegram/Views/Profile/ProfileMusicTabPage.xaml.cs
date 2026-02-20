//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileMusicTabPage : ProfileTabPage
    {
        public ProfileMusicTabPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (IsProfile)
            {
                FindName(nameof(SearchRoot));
            }
            else
            {
                ScrollingHost.Style = BootStrapper.Current.Resources["DefaultListViewStyle"] as Style;
                ScrollingHost.Padding = new Thickness(0);
                ScrollingHost.ItemContainerCornerRadius = new CornerRadius(0);
            }

            if (ViewModel.Music.Empty())
            {
                ScrollingHost.ItemContainerTransitions.Add(new EntranceThemeTransition { IsStaggeringEnabled = false });
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            try
            {
                if (args.InRecycleQueue || ViewModel == null)
                {
                    return;
                }
                else if (args.ItemContainer.ContentTemplateRoot is SharedAudioCell cell)
                {
                    if (!IsProfile)
                    {
                        args.ItemContainer.BorderThickness = new Thickness(0);
                        args.ItemContainer.Background = null;
                    }

                    if (args.Item is MessageWithOwner message)
                    {
                        cell.UpdateMessage(message);
                    }
                    else
                    {
                        cell.Hide();
                    }

                    args.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
        }
    }
}
