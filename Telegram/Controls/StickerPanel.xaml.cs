//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class StickerPanel : UserControl
    {
        public new FrameworkElement Shadow => ShadowElement;
        public FrameworkElement Presenter => BackgroundElement;

        public Action<object> EmojiClick { get; set; }

        public Action<Sticker, bool> StickerClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Sticker>> StickerContextRequested;
        public event EventHandler ChoosingSticker;

        public Action<Animation> AnimationClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Animation>> AnimationContextRequested;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private bool _initialized;

        public StickerPanel()
        {
            InitializeComponent();

            var header = DropShadowEx.Attach(HeaderSeparator);
            var shadow = DropShadowEx.Attach(ShadowElement);

            header.Clip = header.Compositor.CreateInsetClip(0, -40, 0, 40);

            switch (SettingsService.Current.Stickers.SelectedTab)
            {
                case StickersTab.Emoji:
                    Pivot.SelectedIndex = 0;
                    break;
                case StickersTab.Animations:
                    Pivot.SelectedIndex = 1;
                    break;
                case StickersTab.Stickers:
                    Pivot.SelectedIndex = 2;
                    break;
            }
        }

        private void Emojis_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                EmojiClick?.Invoke(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                EmojiClick?.Invoke((Sticker)sticker);
            }
        }

        private void Stickers_ItemClick(Sticker obj, bool fromStickerSet)
        {
            StickerClick?.Invoke(obj, fromStickerSet);
        }

        private void Animations_ItemClick(Animation obj)
        {
            AnimationClick?.Invoke(obj);
        }

        private IEnumerable<IDrawer> GetDrawers()
        {
            yield return EmojisRoot;
            yield return AnimationsRoot;
            yield return StickersRoot;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAtIndex(ViewModel.Chat, Pivot.SelectedIndex, /* unsure here */ false);
        }

        private void LoadAtIndex(Chat chat, int index, bool unload)
        {
            if (index == 0)
            {
                if (unload)
                {
                    UnloadAtIndex(1);
                    UnloadAtIndex(2);
                }

                if (EmojisRoot == null)
                {
                    FindName(nameof(EmojisRoot));
                    EmojisRoot.DataContext = EmojiDrawerViewModel.GetForCurrentView(ViewModel.SessionId);
                }

                EmojisRoot.Activate(chat);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Emoji;
            }
            else if (index == 1)
            {
                if (unload)
                {
                    UnloadAtIndex(0);
                    UnloadAtIndex(2);
                }

                if (AnimationsRoot == null)
                {
                    FindName(nameof(AnimationsRoot));
                    AnimationsRoot.DataContext = AnimationDrawerViewModel.GetForCurrentView(ViewModel.SessionId);
                    AnimationsRoot.ItemClick = Animations_ItemClick;
                    AnimationsRoot.ItemContextRequested += (s, args) => AnimationContextRequested?.Invoke(s, args);
                }

                AnimationsRoot.Activate(chat);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Animations;
            }
            else if (index == 2)
            {
                if (unload)
                {
                    UnloadAtIndex(0);
                    UnloadAtIndex(1);
                }

                if (StickersRoot == null)
                {
                    FindName(nameof(StickersRoot));
                    StickersRoot.DataContext = StickerDrawerViewModel.GetForCurrentView(ViewModel.SessionId);
                    StickersRoot.ItemClick = Stickers_ItemClick;
                    StickersRoot.ItemContextRequested += (s, args) => StickerContextRequested?.Invoke(s, args);
                    StickersRoot.ChoosingItem += (s, args) => ChoosingSticker?.Invoke(s, args);
                }

                StickersRoot.Activate(chat);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Stickers;
            }
        }

        private void UnloadAtIndex(int index)
        {
            if (index == 0 && EmojisRoot != null)
            {
                EmojisRoot.Deactivate();
                UnloadObject(EmojisRoot);
            }
            else if (index == 1 && AnimationsRoot != null)
            {
                var viewModel = AnimationsRoot.DataContext as AnimationDrawerViewModel;

                AnimationsRoot.Deactivate();
                UnloadObject(AnimationsRoot);

                if (viewModel != null)
                {
                    viewModel.Search(string.Empty);
                }
            }
            else if (index == 2 && StickersRoot != null)
            {
                var viewModel = StickersRoot.DataContext as StickerDrawerViewModel;

                StickersRoot.Deactivate();
                UnloadObject(StickersRoot);

                if (viewModel != null)
                {
                    viewModel.Search(string.Empty, false);
                }
            }
        }

        public void UpdateChatPermissions(IClientService clientService, Chat chat)
        {
            var emojisRights = DialogViewModel.VerifyRights(clientService, chat, x => x.CanSendBasicMessages, Strings.Resources.GlobalSendMessageRestricted, Strings.Resources.SendMessageRestrictedForever, Strings.Resources.SendMessageRestricted, out string emojisLabel);
            var stickersRights = DialogViewModel.VerifyRights(clientService, chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachStickersRestricted, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted, out string stickersLabel);
            var animationsRights = DialogViewModel.VerifyRights(clientService, chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachGifRestricted, Strings.Resources.AttachGifRestrictedForever, Strings.Resources.AttachGifRestricted, out string animationsLabel);

            EmojisPanel.Visibility = emojisRights ? Visibility.Collapsed : Visibility.Visible;
            EmojisPermission.Visibility = emojisRights ? Visibility.Visible : Visibility.Collapsed;
            EmojisPermission.Text = emojisLabel ?? string.Empty;

            StickersPanel.Visibility = stickersRights ? Visibility.Collapsed : Visibility.Visible;
            StickersPermission.Visibility = stickersRights ? Visibility.Visible : Visibility.Collapsed;
            StickersPermission.Text = stickersLabel ?? string.Empty;

            AnimationsPanel.Visibility = animationsRights ? Visibility.Collapsed : Visibility.Visible;
            AnimationsPermission.Visibility = animationsRights ? Visibility.Visible : Visibility.Collapsed;
            AnimationsPermission.Text = animationsLabel ?? string.Empty;
        }

        public void Activate()
        {
            if (!_initialized)
            {
                _initialized = true;
                Pivot.SelectionChanged += OnSelectionChanged;
            }

            LoadAtIndex(ViewModel.Chat, Pivot.SelectedIndex, /* unsure here */ false);
        }

        public void Deactivate()
        {
            for (int i = 0; i < 3; i++)
            {
                UnloadAtIndex(i);
            }
        }

        public void UnloadVisibleItems()
        {
            foreach (var drawer in GetDrawers())
            {
                drawer?.UnloadVisibleItems();
            }
        }

        public void LoadVisibleItems()
        {
            foreach (var drawer in GetDrawers())
            {
                drawer?.LoadVisibleItems();
            }
        }
    }

    public interface IDrawer
    {
        void Activate(Chat chat, EmojiSearchType type = EmojiSearchType.Default);
        void Deactivate();

        void LoadVisibleItems();
        void UnloadVisibleItems();

        StickersTab Tab { get; }
    }
}
