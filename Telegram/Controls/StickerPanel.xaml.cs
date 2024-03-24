//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
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

        public event EventHandler<StickerDrawerItemClickEventArgs> StickerClick;
        public event EventHandler<ItemContextRequestedEventArgs<Sticker>> StickerContextRequested;
        public event EventHandler ChoosingSticker;

        public event EventHandler<ItemClickEventArgs> AnimationClick;
        public event EventHandler<ItemContextRequestedEventArgs<Animation>> AnimationContextRequested;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public int SessionId
        {
            get
            {
                if (DataContext is ViewModelBase viewModel)
                {
                    return viewModel.SessionId;
                }

                return int.MaxValue;
            }
        }

        private int _prevIndex = -1;

        public StickerPanel()
        {
            InitializeComponent();

            var header = DropShadowEx.Attach(HeaderSeparator);
            var shadow = DropShadowEx.Attach(ShadowElement);

            header.Clip = header.Compositor.CreateInsetClip(0, -40, 0, 40);
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
                    EmojisRoot.LayoutUpdated += EmojisRoot_LayoutUpdated;
                    EmojisRoot.DataContext = EmojiDrawerViewModel.Create(SessionId);
                }
                else
                {
                    Show(Tab0, _prevIndex > index, 0);
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
                    AnimationsRoot.LayoutUpdated += AnimationsRoot_LayoutUpdated;
                    AnimationsRoot.DataContext = AnimationDrawerViewModel.Create(SessionId);
                    AnimationsRoot.ItemClick += AnimationClick;
                    AnimationsRoot.ItemContextRequested += AnimationContextRequested;
                }
                else
                {
                    Show(Tab1, _prevIndex > index, 1);
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
                    StickersRoot.LayoutUpdated += StickersRoot_LayoutUpdated;
                    StickersRoot.DataContext = StickerDrawerViewModel.Create(SessionId);
                    StickersRoot.ItemClick += StickerClick;
                    StickersRoot.ItemContextRequested += StickerContextRequested;
                    StickersRoot.ChoosingItem += ChoosingSticker;
                    StickersRoot.SettingsClick += SettingsClick;
                }
                else
                {
                    Show(Tab2, _prevIndex > index, 2);
                    StickersRoot.LoadVisibleItems();
                }

                StickersRoot.Activate(chat);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Stickers;
            }

            _prevIndex = index;
            Navigation.SelectionChanged -= OnSelectionChanged;
            Navigation.SelectedIndex = index;
            Navigation.SelectionChanged += OnSelectionChanged;
        }

        private void Show(UIElement element, bool leftToRight, int index)
        {
            if (_prevIndex == -1)
            {
                return;
            }

            var visualIn = ElementComposition.GetElementVisual(element);
            var offsetIn = visualIn.Compositor.CreateVector3KeyFrameAnimation();
            offsetIn.InsertKeyFrame(0, new System.Numerics.Vector3(leftToRight ? -48 : 48, 0, 0));
            offsetIn.InsertKeyFrame(1, new System.Numerics.Vector3());
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
                StickersRoot.SettingsClick -= SettingsClick;
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

        private void EmojisRoot_LayoutUpdated(object sender, object e)
        {
            if (sender is FrameworkElement element)
            {
                element.LayoutUpdated -= EmojisRoot_LayoutUpdated;
                Show(Tab0, _prevIndex > 0, 0);
            }
        }

        private void AnimationsRoot_LayoutUpdated(object sender, object e)
        {
            if (sender is FrameworkElement element)
            {
                element.LayoutUpdated -= AnimationsRoot_LayoutUpdated;
                Show(Tab1, _prevIndex > 1, 1);
            }
        }

        private void StickersRoot_LayoutUpdated(object sender, object e)
        {
            if (sender is FrameworkElement element)
            {
                element.LayoutUpdated -= StickersRoot_LayoutUpdated;
                Show(Tab2, _prevIndex > 2, 2);
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
