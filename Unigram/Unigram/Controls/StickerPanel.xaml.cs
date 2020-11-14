using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
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
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public sealed partial class StickerPanel : UserControl, IFileDelegate
    {
        public FrameworkElement Presenter => BackgroundElement;

        public Action<string> EmojiClick { get; set; }

        public Action<Sticker> StickerClick { get; set; }
        public event TypedEventHandler<UIElement, ContextRequestedEventArgs> StickerContextRequested;

        public Action<Animation> AnimationClick { get; set; }
        public event TypedEventHandler<UIElement, ContextRequestedEventArgs> AnimationContextRequested;

        private StickersPanelMode _widget;

        public StickerPanel()
        {
            InitializeComponent();
            DataContext = new object();

            var shadow = DropShadowEx.Attach(HeaderSeparator, 20, 0.25f);
            shadow.RelativeSizeAdjustment = Vector2.One;

            var protoService = TLContainer.Current.Resolve<IProtoService>();

            AnimationsRoot.DataContext = AnimationDrawerViewModel.GetForCurrentView(protoService.SessionId);
            AnimationsRoot.ItemClick = Animations_ItemClick;
            AnimationsRoot.ItemContextRequested += (s, args) => AnimationContextRequested?.Invoke(s, args);

            StickersRoot.DataContext = StickerDrawerViewModel.GetForCurrentView(protoService.SessionId);
            StickersRoot.ItemClick = Stickers_ItemClick;
            StickersRoot.ItemContextRequested += (s, args) => StickerContextRequested?.Invoke(s, args);

            EmojisRoot.DataContext = StickersRoot.DataContext;

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

            EmojisRoot?.SetView(mode);
            VisualStateManager.GoToState(this, mode == StickersPanelMode.Overlay
                ? "FilledState"
                : mode == StickersPanelMode.Sidebar
                ? "SidebarState"
                : "NarrowState", false);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = Pivot.GetScrollViewer();
            if (scrollViewer != null)
            {
                scrollViewer.DirectManipulationStarted += ScrollViewer_DirectManipulationStarted;
            }
        }

        private void ScrollViewer_DirectManipulationStarted(object sender, object e)
        {
            var transform = Pivot.TransformToVisual(Window.Current.Content as UIElement);
            var point = transform.TransformPoint(new Point());

            var rect = new Rect(point.X, point.Y, Pivot.ActualWidth, 48);
            if (rect.Contains(Window.Current.CoreWindow.PointerPosition))
            {
                Pivot.GetScrollViewer().CancelDirectManipulations();
            }
        }

        public void UpdateFile(File file)
        {
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            StickersRoot.UpdateFile(file);
            AnimationsRoot.UpdateFile(file);
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

        private IDrawer GetActiveDrawer()
        {
            switch (Pivot.SelectedIndex)
            {
                case 0:
                    return EmojisRoot;
                case 1:
                    return AnimationsRoot;
                case 2:
                default:
                    return StickersRoot;
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Pivot.SelectedIndex == 0 && EmojisRoot == null)
            {
                FindName(nameof(Emojis));
                EmojisRoot.SetView(_widget);
            }

            var active = GetActiveDrawer();

            foreach (var drawer in GetDrawers())
            {
                if (drawer == active)
                {
                    drawer.Activate();
                }
                else
                {
                    drawer?.Deactivate();
                }
            }

            SettingsService.Current.Stickers.SelectedTab = active.Tab;
        }

        public async void Refresh()
        {
            // TODO: memes
            //StickersRoot.ViewModel.SyncStickers(null);
            //AnimationsRoot.ViewModel.Update();

            await Task.Delay(100);
            Pivot_SelectionChanged(null, null);
        }

        public void UpdateChatPermissions(ICacheService cacheService, Chat chat)
        {
            var emojisRights = DialogViewModel.VerifyRights(cacheService, chat, x => x.CanSendMessages, Strings.Resources.GlobalSendMessageRestricted, Strings.Resources.SendMessageRestrictedForever, Strings.Resources.SendMessageRestricted, out string emojisLabel);
            var stickersRights = DialogViewModel.VerifyRights(cacheService, chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachStickersRestricted, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted, out string stickersLabel);
            var animationsRights = DialogViewModel.VerifyRights(cacheService, chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachGifRestricted, Strings.Resources.AttachGifRestrictedForever, Strings.Resources.AttachGifRestricted, out string animationsLabel);

            EmojisRoot.Visibility = emojisRights ? Visibility.Collapsed : Visibility.Visible;
            EmojisPermission.Visibility = emojisRights ? Visibility.Visible : Visibility.Collapsed;
            EmojisPermission.Text = emojisLabel ?? string.Empty;

            StickersRoot.Visibility = stickersRights ? Visibility.Collapsed : Visibility.Visible;
            StickersPermission.Visibility = stickersRights ? Visibility.Visible : Visibility.Collapsed;
            StickersPermission.Text = stickersLabel ?? string.Empty;

            AnimationsRoot.Visibility = animationsRights ? Visibility.Collapsed : Visibility.Visible;
            AnimationsPermission.Visibility = animationsRights ? Visibility.Visible : Visibility.Collapsed;
            AnimationsPermission.Text = animationsLabel ?? string.Empty;
        }

        public void Deactivate()
        {
            foreach (var drawer in GetDrawers())
            {
                drawer?.Deactivate();
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
