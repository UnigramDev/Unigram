//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Views
{
    public sealed partial class RecentChatsView : UserControl
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly Popup _popup;
        private readonly bool _fromStart;

        private bool _hasCurrentChat;

        public RecentChatsView(IClientService clientService, INavigationService navigationService, Popup popup, bool fromStart)
        {
            _clientService = clientService;
            _navigationService = navigationService;
            _fromStart = fromStart;

            _popup = popup;
            _popup.XamlRoot.Content.PreviewKeyDown += OnKeyDown;
            _popup.XamlRoot.Content.PreviewKeyUp += OnKeyUp;
            _popup.Closed += OnClosed;

            InitializeComponent();
            InitializeChats(clientService);

            Loaded += OnLoaded;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                RootGrid.Shadow = themeShadow;
                RootGrid.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(ShadowReceiver);
            }
        }

        private void OnClosed(object sender, object e)
        {
            XamlRoot.Content.PreviewKeyDown -= OnKeyDown;
            XamlRoot.Content.PreviewKeyUp -= OnKeyUp;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ScrollingHost.Items.Count > 0)
            {
                SelectFirstChat();
            }
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                var modifiers = WindowContext.KeyModifiers();
                if (modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift))
                {
                    MoveLeft();
                    e.Handled = true;
                }
                else if (modifiers == VirtualKeyModifiers.Control)
                {
                    MoveRight();
                    e.Handled = true;
                }
            }
            else if (e.Key == VirtualKey.Left)
            {
                MoveLeft();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Up)
            {
                MoveTop();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Right)
            {
                MoveRight();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down)
            {
                MoveDown();
                e.Handled = true;
            }
        }

        private void MoveLeft()
        {
            if (ScrollingHost.SelectedIndex > 0)
            {
                ScrollingHost.SelectedIndex--;
            }
            else
            {
                ScrollingHost.SelectedIndex = ScrollingHost.Items.Count - 1;
            }
        }

        private void MoveTop()
        {
            var width = (int)(ScrollingHost.ActualWidth / (80 + 2 + 2));
            if (width == 0)
            {
                return;
            }

            var height = (ScrollingHost.Items.Count + width - 1) / width;

            int y = ScrollingHost.SelectedIndex / width;
            int x = ScrollingHost.SelectedIndex % width;

            if (y == 0)
            {
                y = height - 1;
            }
            else
            {
                y--;
            }

            int index = y * width + x;

            ScrollingHost.SelectedIndex = Math.Clamp(index, 0, ScrollingHost.Items.Count - 1);
        }

        private void MoveRight()
        {
            if (ScrollingHost.SelectedIndex < ScrollingHost.Items.Count - 1)
            {
                ScrollingHost.SelectedIndex++;
            }
            else
            {
                ScrollingHost.SelectedIndex = 0;
            }
        }

        private void MoveDown()
        {
            var width = (int)(ScrollingHost.ActualWidth / (80 + 2 + 2));
            if (width == 0)
            {
                return;
            }

            var height = (ScrollingHost.Items.Count + width - 1) / width;

            int y = ScrollingHost.SelectedIndex / width;
            int x = ScrollingHost.SelectedIndex % width;

            if (y == height - 1)
            {
                y = 0;
            }
            else
            {
                y++;
            }

            int index = y * width + x;

            ScrollingHost.SelectedIndex = Math.Clamp(index, 0, ScrollingHost.Items.Count - 1);
        }

        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control)
            {
                _popup.IsOpen = false;

                if (ScrollingHost.SelectedItem is Chat chat)
                {
                    _navigationService.NavigateToChat(chat, force: false, clearBackStack: true);
                    e.Handled = true;
                }
            }
        }

        private void InitializeChats(IClientService clientService)
        {
            var items = _clientService.GetRecentlyOpenedChats();
            var current = _navigationService.GetChatFromBackStack(true);

            if (clientService.TryGetChat(current.ChatId, out Chat currentChat))
            {
                _hasCurrentChat = true;

                if (items.Contains(currentChat))
                {
                    items.Remove(currentChat);
                }

                items.Insert(0, currentChat);
            }

            ScrollingHost.ItemsSource = items;

            if (IsLoaded)
            {
                SelectFirstChat();
            }
        }

        private void SelectFirstChat()
        {
            Focus(FocusState.Pointer);

            if (_hasCurrentChat)
            {
                ScrollingHost.SelectedIndex = _fromStart ? Math.Min(1, ScrollingHost.Items.Count - 1) : ScrollingHost.Items.Count - 1;
            }
            else
            {
                ScrollingHost.SelectedIndex = _fromStart ? 0 : ScrollingHost.Items.Count - 1;
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem
                {
                    ContentTemplate = sender.ItemTemplate,
                    UseSystemFocusVisuals = false,
                    Margin = new Thickness(2)
                };
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is Chat chat)
            {
                var photo = content.Children[0] as ProfilePicture;
                var textBlock = content.Children[1] as TextBlock;

                photo.Source = ProfilePictureSource.Chat(_clientService, chat);
                textBlock.Text = chat.Title;

                AutomationProperties.SetName(args.ItemContainer, chat.Title);

                args.Handled = true;
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Chat chat)
            {
                _popup.IsOpen = false;
                _navigationService.NavigateToChat(chat, force: false, clearBackStack: true);
            }
        }
    }
}
