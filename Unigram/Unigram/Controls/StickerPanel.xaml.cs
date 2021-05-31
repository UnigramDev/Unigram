using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Drawers;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Drawers;
using Unigram.Views;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public sealed partial class StickerPanel : UserControl, IFileDelegate
    {
        public FrameworkElement Presenter => BackgroundElement;

        public Action<string> EmojiClick { get; set; }

        public Action<Sticker> StickerClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Sticker>> StickerContextRequested;

        public Action<Animation> AnimationClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Animation>> AnimationContextRequested;

        private StickersPanelMode _widget;

        public StickerPanel()
        {
            InitializeComponent();
            DataContext = new object();

            DropShadowEx.Attach(HeaderSeparator);

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

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                BackgroundElement.Shadow = themeShadow;
                BackgroundElement.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(ShadowElement);
            }
        }

        public void SetView(StickersPanelMode mode)
        {
            _widget = mode;
            VisualStateManager.GoToState(this, mode == StickersPanelMode.Overlay
                ? "FilledState"
                : mode == StickersPanelMode.Sidebar
                ? "SidebarState"
                : "NarrowState", false);
        }

        public void UpdateFile(File file)
        {
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            StickersRoot?.UpdateFile(file);
            AnimationsRoot?.UpdateFile(file);
        }

        private void Emojis_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                EmojiClick?.Invoke(emoji.Value);
            }
        }

        private void Stickers_ItemClick(Sticker obj)
        {
            StickerClick?.Invoke(obj);
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

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAtIndex(Pivot.SelectedIndex, /* unsure here */ false);
        }

        private void LoadAtIndex(int index, bool unload)
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
                    EmojisRoot.DataContext = AnimationDrawerViewModel.GetForCurrentView(TLContainer.Current.Resolve<IProtoService>().SessionId);
                }

                EmojisRoot.Activate();
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
                    AnimationsRoot.DataContext = AnimationDrawerViewModel.GetForCurrentView(TLContainer.Current.Resolve<IProtoService>().SessionId);
                    AnimationsRoot.ItemClick = Animations_ItemClick;
                    AnimationsRoot.ItemContextRequested += (s, args) => AnimationContextRequested?.Invoke(s, args);
                }

                AnimationsRoot.Activate();
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
                    StickersRoot.DataContext = StickerDrawerViewModel.GetForCurrentView(TLContainer.Current.Resolve<IProtoService>().SessionId);
                    StickersRoot.ItemClick = Stickers_ItemClick;
                    StickersRoot.ItemContextRequested += (s, args) => StickerContextRequested?.Invoke(s, args);
                }

                StickersRoot.Activate();
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
                AnimationsRoot.Deactivate();
                UnloadObject(AnimationsRoot);
            }
            else if (index == 2 && StickersRoot != null)
            {
                StickersRoot.Deactivate();
                UnloadObject(StickersRoot);
            }
        }

        public void UpdateChatPermissions(ICacheService cacheService, Chat chat)
        {
            var emojisRights = DialogViewModel.VerifyRights(cacheService, chat, x => x.CanSendMessages, Strings.Resources.GlobalSendMessageRestricted, Strings.Resources.SendMessageRestrictedForever, Strings.Resources.SendMessageRestricted, out string emojisLabel);
            var stickersRights = DialogViewModel.VerifyRights(cacheService, chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachStickersRestricted, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted, out string stickersLabel);
            var animationsRights = DialogViewModel.VerifyRights(cacheService, chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachGifRestricted, Strings.Resources.AttachGifRestrictedForever, Strings.Resources.AttachGifRestricted, out string animationsLabel);

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
            Pivot_SelectionChanged(null, null);
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
        void Activate();
        void Deactivate();

        void LoadVisibleItems();
        void UnloadVisibleItems();

        StickersTab Tab { get; }
    }
}
