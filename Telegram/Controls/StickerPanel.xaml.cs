//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public sealed partial class StickerPanel : UserControl
    {
        public new FrameworkElement Shadow => ShadowElement;
        public FrameworkElement Presenter => BackgroundElement;

        public event EventHandler SettingsClick;

        public Action<object> EmojiClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<StickerViewModel>> EmojiContextRequested;

        public event EventHandler<StickerDrawerItemClickEventArgs> StickerClick;
        public event EventHandler<ItemContextRequestedEventArgs<Sticker>> StickerContextRequested;
        public event EventHandler ChoosingSticker;

        public event EventHandler<ItemClickEventArgs> AnimationClick;
        public event EventHandler<ItemContextRequestedEventArgs<Animation>> AnimationContextRequested;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ISession Session
        {
            get
            {
                if (DataContext is ViewModelBase viewModel)
                {
                    return viewModel.Session;
                }

                // TODO: verify
                return null;
            }
        }

        private int _prevIndex = -1;

        public StickerPanel()
        {
            InitializeComponent();

            var header = VisualUtilities.DropShadow(HeaderSeparator);
            var shadow = VisualUtilities.DropShadow(ShadowElement);

            header.Clip = header.Compositor.CreateInsetClip(0, -40, 0, 40);
        }

        private void Emojis_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
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

        private IEnumerable<IDrawer> GetDrawers()
        {
            yield return EmojisRoot;
            yield return AnimationsRoot;
            yield return StickersRoot;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAtIndex(ViewModel?.Chat, Navigation.SelectedIndex, /* unsure here */ false);
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
                else
                {
                    Tab1.Visibility = Visibility.Collapsed;
                    Tab2.Visibility = Visibility.Collapsed;

                    AnimationsRoot?.UnloadVisibleItems();
                    StickersRoot?.UnloadVisibleItems();
                }

                Tab0.Visibility = Visibility.Visible;

                if (EmojisRoot == null)
                {
                    FindName(nameof(EmojisRoot));
                    EmojisRoot.DataContext = EmojiDrawerViewModel.Create(Session);
                    EmojisRoot.ItemContextRequested += EmojiContextRequested;
                }
                else
                {
                    if (EmojisRoot.IsLoaded)
                    {
                        Show(Tab0, _prevIndex, _prevIndex = index);
                    }

                    EmojisRoot.LoadVisibleItems();
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
                else
                {
                    Tab0.Visibility = Visibility.Collapsed;
                    Tab2.Visibility = Visibility.Collapsed;

                    EmojisRoot?.UnloadVisibleItems();
                    StickersRoot?.UnloadVisibleItems();
                }

                Tab1.Visibility = Visibility.Visible;

                if (AnimationsRoot == null)
                {
                    FindName(nameof(AnimationsRoot));
                    AnimationsRoot.DataContext = AnimationDrawerViewModel.Create(Session);
                    AnimationsRoot.ItemClick += AnimationClick;
                    AnimationsRoot.ItemContextRequested += AnimationContextRequested;
                }
                else
                {
                    if (AnimationsRoot.IsLoaded)
                    {
                        Show(Tab1, _prevIndex, _prevIndex = index);
                    }

                    AnimationsRoot.LoadVisibleItems();
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
                else
                {
                    Tab0.Visibility = Visibility.Collapsed;
                    Tab1.Visibility = Visibility.Collapsed;

                    EmojisRoot?.UnloadVisibleItems();
                    AnimationsRoot?.UnloadVisibleItems();
                }

                Tab2.Visibility = Visibility.Visible;

                if (StickersRoot == null)
                {
                    FindName(nameof(StickersRoot));
                    StickersRoot.DataContext = StickerDrawerViewModel.Create(Session);
                    StickersRoot.ItemClick += StickerClick;
                    StickersRoot.ItemContextRequested += StickerContextRequested;
                    StickersRoot.ChoosingItem += ChoosingSticker;
                }
                else
                {
                    if (StickersRoot.IsLoaded)
                    {
                        Show(Tab2, _prevIndex, _prevIndex = index);
                    }

                    StickersRoot.LoadVisibleItems();
                }

                StickersRoot.Activate(chat);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Stickers;
            }

            Navigation.SelectionChanged -= OnSelectionChanged;
            Navigation.SelectedIndex = index;
            Navigation.SelectionChanged += OnSelectionChanged;
        }

        private void Show(UIElement element, int prevIndex, int index)
        {
            Settings.Visibility = index != 1
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled || prevIndex == index || prevIndex == -1)
            {
                return;
            }

            var leftToRight = prevIndex > index;

            var visualIn = ElementComposition.GetElementVisual(element);
            var offsetIn = visualIn.Compositor.CreateVector3KeyFrameAnimation();
            offsetIn.InsertKeyFrame(0, new Vector3(leftToRight ? -48 : 48, 0, 0));
            offsetIn.InsertKeyFrame(1, new Vector3());
            offsetIn.Duration = Constants.SoftAnimation;

            var opacityIn = visualIn.Compositor.CreateScalarKeyFrameAnimation();
            opacityIn.InsertKeyFrame(0, 0);
            opacityIn.InsertKeyFrame(1, 1);
            opacityIn.Duration = Constants.SoftAnimation;

            visualIn.StartAnimation("Offset", offsetIn);
            visualIn.StartAnimation("Opacity", opacityIn);
        }

        private void UnloadAtIndex(int index)
        {
            if (index == 0 && EmojisRoot != null)
            {
                EmojisRoot.Deactivate();
                EmojisRoot.DataContext = null;
                EmojisRoot.ItemContextRequested -= EmojiContextRequested;
                UnloadObject(EmojisRoot);

                Tab0.Visibility = Visibility.Collapsed;
            }
            else if (index == 1 && AnimationsRoot != null)
            {
                var viewModel = AnimationsRoot.DataContext as AnimationDrawerViewModel;

                AnimationsRoot.Deactivate();
                AnimationsRoot.DataContext = null;
                AnimationsRoot.ItemClick -= AnimationClick;
                AnimationsRoot.ItemContextRequested -= AnimationContextRequested;
                UnloadObject(AnimationsRoot);

                Tab1.Visibility = Visibility.Collapsed;

                viewModel?.Search(string.Empty);
            }
            else if (index == 2 && StickersRoot != null)
            {
                var viewModel = StickersRoot.DataContext as StickerDrawerViewModel;

                StickersRoot.Deactivate();
                StickersRoot.DataContext = null;
                StickersRoot.ItemClick -= StickerClick;
                StickersRoot.ItemContextRequested -= StickerContextRequested;
                StickersRoot.ChoosingItem -= ChoosingSticker;
                UnloadObject(StickersRoot);

                Tab2.Visibility = Visibility.Collapsed;

                viewModel?.Search(string.Empty, false);
            }
        }

        private bool _emojisRights;
        private bool _stickersRights;
        private bool _animationsRights;

        public void UpdateChatPermissions(IClientService clientService, Chat chat)
        {
            var emojisRights = DialogViewModel.VerifyRights(clientService, chat, x => x.CanSendBasicMessages, Strings.GlobalSendMessageRestricted, Strings.SendMessageRestrictedForever, Strings.SendMessageRestricted, out string emojisLabel);
            var stickersRights = DialogViewModel.VerifyRights(clientService, chat, x => x.CanSendOtherMessages, Strings.GlobalAttachStickersRestricted, Strings.AttachStickersRestrictedForever, Strings.AttachStickersRestricted, out string stickersLabel);
            var animationsRights = DialogViewModel.VerifyRights(clientService, chat, x => x.CanSendOtherMessages, Strings.GlobalAttachGifRestricted, Strings.AttachGifRestrictedForever, Strings.AttachGifRestricted, out string animationsLabel);

            if (_emojisRights != emojisRights || emojisRights)
            {
                _emojisRights = emojisRights;
                EmojisPanel.Visibility = emojisRights ? Visibility.Collapsed : Visibility.Visible;
                EmojisPermission.Visibility = emojisRights ? Visibility.Visible : Visibility.Collapsed;
                EmojisPermission.Text = emojisLabel ?? string.Empty;
            }

            if (_stickersRights != stickersRights || stickersRights)
            {
                _stickersRights = stickersRights;
                StickersPanel.Visibility = stickersRights ? Visibility.Collapsed : Visibility.Visible;
                StickersPermission.Visibility = stickersRights ? Visibility.Visible : Visibility.Collapsed;
                StickersPermission.Text = stickersLabel ?? string.Empty;
            }

            if (_animationsRights != animationsRights || animationsRights)
            {
                _animationsRights = animationsRights;
                AnimationsPanel.Visibility = animationsRights ? Visibility.Collapsed : Visibility.Visible;
                AnimationsPermission.Visibility = animationsRights ? Visibility.Visible : Visibility.Collapsed;
                AnimationsPermission.Text = animationsLabel ?? string.Empty;
            }
        }

        public void Activate()
        {
            switch (SettingsService.Current.Stickers.SelectedTab)
            {
                case StickersTab.Emoji:
                    LoadAtIndex(ViewModel?.Chat, 0, /* unsure here */ false);
                    break;
                case StickersTab.Animations:
                    LoadAtIndex(ViewModel?.Chat, 1, /* unsure here */ false);
                    break;
                case StickersTab.Stickers:
                    LoadAtIndex(ViewModel?.Chat, 2, /* unsure here */ false);
                    break;
            }
        }

        public void Deactivate()
        {
            for (int i = 0; i < 3; i++)
            {
                UnloadAtIndex(i);
            }

            _prevIndex = -1;
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

        private void EmojisRoot_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= EmojisRoot_Loaded;
                Show(Tab0, _prevIndex, _prevIndex = 0);
            }
        }

        private void AnimationsRoot_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= AnimationsRoot_Loaded;
                Show(Tab1, _prevIndex, _prevIndex = 1);
            }
        }

        private void StickersRoot_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= StickersRoot_Loaded;
                Show(Tab2, _prevIndex, _prevIndex = 2);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsClick?.Invoke(this, EventArgs.Empty);
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
