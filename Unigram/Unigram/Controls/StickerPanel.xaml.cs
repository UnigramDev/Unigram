using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.Views;
using Unigram.Views.Settings;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public sealed partial class StickerPanel : UserControl, IFileDelegate
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public FrameworkElement Presenter => BackgroundElement;

        public Action<string> EmojiClick { get; set; }
        public Action<Sticker> StickerClick { get; set; }
        public Action<Animation> AnimationClick { get; set; }

        private StickersPanelMode _widget;

        public StickerPanel()
        {
            InitializeComponent();

            var shadow1 = DropShadowEx.Attach(HeaderSeparator, 20, 0.25f);

            HeaderSeparator.SizeChanged += (s, args) =>
            {
                shadow1.Size = args.NewSize.ToVector2();
            };

            AnimationsRoot.AnimationClick = Animations_ItemClick;
            StickersRoot.StickerClick = Stickers_ItemClick;

            switch (SettingsService.Current.Stickers.SelectedTab)
            {
                case Services.Settings.StickersTab.Emoji:
                    Pivot.SelectedIndex = 0;
                    break;
                case Services.Settings.StickersTab.Animations:
                    Pivot.SelectedIndex = 1;
                    break;
                case Services.Settings.StickersTab.Stickers:
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

            Emojis?.SetView(mode);
            VisualStateManager.GoToState(this, mode == StickersPanelMode.Overlay
                ? "FilledState"
                : mode == StickersPanelMode.Sidebar
                ? "SidebarState"
                : "NarrowState", false);
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null) Bindings.Update();
            if (ViewModel == null) Bindings.StopTracking();
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
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
            yield return Emojis;
            yield return AnimationsRoot;
            yield return StickersRoot;
        }

        private IDrawer GetActiveDrawer()
        {
            switch (Pivot.SelectedIndex)
            {
                case 0:
                    return Emojis;
                case 1:
                    return AnimationsRoot;
                case 2:
                default:
                    return StickersRoot;
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Pivot.SelectedIndex == 0 && Emojis == null)
            {
                FindName(nameof(Emojis));
                Emojis.SetView(_widget);
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

            if (ViewModel != null)
            {
                ViewModel.Settings.Stickers.SelectedTab = active.Tab;
            }
        }

        public async void Refresh()
        {
            // TODO: memes

            await Task.Delay(100);
            Pivot_SelectionChanged(null, null);
        }

        private void GroupStickers_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GroupStickersCommand.Execute(null);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(SettingsStickersPage));
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Stickers.InstallCommand.Execute(((Button)sender).DataContext);
        }

        public void UpdateChatPermissions(Chat chat)
        {
            var stickersRights = ViewModel.VerifyRights(chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachStickersRestricted, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted, out string stickersLabel);
            var animationsRights = ViewModel.VerifyRights(chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachGifRestricted, Strings.Resources.AttachGifRestrictedForever, Strings.Resources.AttachGifRestricted, out string animationsLabel);

            StickersRoot.Visibility = stickersRights ? Visibility.Collapsed : Visibility.Visible;
            StickersPermission.Visibility = stickersRights ? Visibility.Visible : Visibility.Collapsed;
            StickersPermission.Text = stickersLabel ?? string.Empty;

            AnimationsRoot.Visibility = animationsRights ? Visibility.Collapsed : Visibility.Visible;
            AnimationsPermission.Visibility = animationsRights ? Visibility.Visible : Visibility.Collapsed;
            AnimationsPermission.Text = animationsLabel ?? string.Empty;
        }

        public void UnloadVisibleItems()
        {
            foreach (var drawer in GetDrawers())
            {
                drawer?.Deactivate();
            }
        }
    }

    public interface IDrawer
    {
        void Activate();
        void Deactivate();

        StickersTab Tab { get; }
    }
}
