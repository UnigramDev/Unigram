//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseProfileColorPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly MessageSender _sender;

        public ChooseProfileColorPopup(IClientService clientService, MessageSender sender)
        {
            InitializeComponent();
            //Title = Strings.UserColorTitle;

            _clientService = clientService;
            _sender = sender;

            NameView.Initialize(clientService, sender);
            ProfileView.Initialize(clientService, sender);

            Navigation.SelectedIndex = 0;
        }

        private async void PurchaseCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_clientService.TryGetChat(_sender, out Chat chat))
            {
                var response = await _clientService.SendAsync(new GetChatBoostStatus(chat.Id));
                if (response is ChatBoostStatus status && status.Level >= _clientService.Options.ChannelCustomAccentColorBoostLevelMin)
                {
                    var changed = false;

                    if (chat.AccentColorId != NameView.ColorId || chat.BackgroundCustomEmojiId != NameView.CustomEmojiId)
                    {
                        _clientService.Send(new SetChatAccentColor(chat.Id, NameView.ColorId, NameView.CustomEmojiId));
                        changed = true;
                    }

                    //if (chat.ProfileAccentColorId != ProfileView.ColorId || chat.ProfileBackgroundCustomEmojiId != ProfileView.CustomEmojiId)
                    //{
                    //    _clientService.Send(new SetChatProfileAccentColor(chat.Id, ProfileView.ColorId, ProfileView.CustomEmojiId));
                    //    changed = true;
                    //}

                    if (changed)
                    {
                        Window.Current.ShowToast(Strings.ChannelColorApplied, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
                    }

                    Hide();
                }
                else
                {
                    // TODO: show boost needed
                }
            }
            else if (_clientService.TryGetUser(_sender, out User user))
            {
                if (_clientService.IsPremium)
                {
                    var changed = false;

                    if (user.AccentColorId != NameView.ColorId || user.BackgroundCustomEmojiId != NameView.CustomEmojiId)
                    {
                        _clientService.Send(new SetAccentColor(NameView.ColorId, NameView.CustomEmojiId));
                        changed = true;
                    }

                    if (user.ProfileAccentColorId != ProfileView.ColorId || user.ProfileBackgroundCustomEmojiId != ProfileView.CustomEmojiId)
                    {
                        _clientService.Send(new SetProfileAccentColor(ProfileView.ColorId, ProfileView.CustomEmojiId));
                        changed = true;
                    }

                    if (changed)
                    {
                        Window.Current.ShowToast(Strings.UserColorApplied, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
                    }

                    Hide();
                }
                else
                {
                    Window.Current.ShowToast(new PremiumFeatureAccentColor());
                }
            }
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UserControl showView = Navigation.SelectedIndex == 0
                ? NameView
                : ProfileView;
            UserControl hideView = Navigation.SelectedIndex == 1
                ? NameView
                : ProfileView;

            showView.Visibility = Visibility.Visible;
            hideView.Visibility = Visibility.Collapsed;

            // TODO: animation?
            //ContentRoot.Height = showView.ActualHeight;

            //var show = ElementCompositionPreview.GetElementVisual(showView);
            //var hide = ElementCompositionPreview.GetElementVisual(hideView);

            //var compositor = show.Compositor;
            //var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            //batch.Completed += (s, args) =>
            //{

            //};

            //batch.End();

            //_currentView = showView;

            PurchaseCommand.Content = showView switch
            {
                ChooseNameColorView name => name.PrimaryButtonText,
                ChooseProfileColorView profile => profile.PrimaryButtonText,
                _ => string.Empty
            };
        }
    }
}
