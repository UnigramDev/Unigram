//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Entities;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    /// <summary>
    /// The attach button of a poll, an option or a quiz's explanation: a paperclip until media is
    /// picked, then the media itself. Its menu offers only what <see cref="Slot"/> can take.
    /// </summary>
    public sealed partial class PollMediaButton : Grid
    {
        private static readonly string[] AudioTypes = new[] { ".mp3", ".m4a", ".ogg", ".oga", ".opus", ".flac", ".wav" };

        private PollMediaDraft _media;
        private FrameworkElement _thumbnail;

        private MenuFlyout _stickerFlyout;
        private StickerDrawer _stickerDrawer;

        public PollMediaButton()
        {
            InitializeComponent();
        }

        public IClientService ClientService { get; set; }

        /// <summary>For the music picker, whose items can be forwarded and saved.</summary>
        public INavigationService NavigationService { get; set; }

        public PollMediaSlot Slot { get; set; }

        public PollMediaDraft Media
        {
            get => _media;
            set => SetMedia(value, false);
        }

        public event EventHandler MediaChanged;

        private int _busy;

        /// <summary>
        /// While its menu, or a picker or popup it opened, is up. They take the focus from the
        /// field the button belongs to, which has to stay up under them.
        /// </summary>
        public bool IsBusy => _busy > 0;

        public event EventHandler IsBusyChanged;

        private void BeginBusy()
        {
            if (_busy++ == 0)
            {
                IsBusyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void EndBusy()
        {
            if (--_busy == 0)
            {
                IsBusyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #region Foreground

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(PollMediaButton), new PropertyMetadata(null, OnForegroundChanged));

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is Brush brush)
            {
                ((PollMediaButton)d).AttachButton.Foreground = brush;
            }
            else
            {
                ((PollMediaButton)d).AttachButton.ClearValue(Control.ForegroundProperty);
            }
        }

        #endregion

        private void SetMedia(PollMediaDraft media, bool notify)
        {
            if (_media == media)
            {
                return;
            }

            _media = media;
            UpdateThumbnail();

            if (notify)
            {
                MediaChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateThumbnail()
        {
            if (_thumbnail != null)
            {
                ThumbnailRoot.Children.Remove(_thumbnail);
                _thumbnail = null;
            }

            var input = _media?.Input;
            if (input == null)
            {
                AttachButton.Glyph = Icons.Attach;
                Thumbnail.Visibility = Visibility.Collapsed;
                return;
            }

            AttachButton.Glyph = null;
            Thumbnail.Visibility = Visibility.Visible;

            if (_media.Preview is ImageSource preview)
            {
                _thumbnail = new Image
                {
                    Source = preview,
                    Stretch = Stretch.UniformToFill
                };
            }
            else if (_media.Sticker is Sticker sticker && ClientService != null)
            {
                _thumbnail = new AnimatedImage
                {
                    Width = 24,
                    Height = 24,
                    FrameSize = new Windows.Foundation.Size(24, 24),
                    DecodeFrameType = DecodePixelType.Logical,
                    Source = DelayedFileSource.FromSticker(ClientService, sticker)
                };
            }
            else if (ClientService != null && input is InputPollMediaLocation or InputPollMediaVenue)
            {
                var location = input is InputPollMediaVenue venue
                    ? venue.Venue.Location
                    : ((InputPollMediaLocation)input).Location;

                var image = new ImageView
                {
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.UniformToFill,
                    XamlRoot = XamlRoot
                };

                image.SetSource(ClientService, location, 24, 24, 0);
                _thumbnail = image;
            }

            if (_thumbnail != null)
            {
                // Between the tile and the glyph.
                ThumbnailRoot.Children.Insert(1, _thumbnail);
            }

            ThumbnailTile.Visibility = _thumbnail == null
                ? Visibility.Visible
                : Visibility.Collapsed;

            ThumbnailGlyph.Text = _thumbnail != null ? string.Empty : input switch
            {
                InputPollMediaLink => Icons.Link,
                InputPollMediaAudio => Icons.MusicNote2,
                _ => Icons.Document
            };
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(PickMedia, Strings.PhotoOrVideo, Icons.Image);

            if (Slot != PollMediaSlot.Option)
            {
                flyout.CreateFlyoutItem(PickDocument, Strings.ChatDocument, Icons.Document);
                flyout.CreateFlyoutItem(PickAudio, Strings.AttachMusic, Icons.MusicNote2);
            }

            flyout.CreateFlyoutItem(PickLocation, Strings.ChatLocation, Icons.Location);

            if (Slot == PollMediaSlot.Option)
            {
                flyout.CreateFlyoutItem(PickSticker, Strings.ChatSticker, Icons.Sticker);
                flyout.CreateFlyoutItem(PickLink, Strings.ChatLink, Icons.Link);
            }

            if (_media != null)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(Remove, Strings.Delete, Icons.Delete, destructive: true);
            }

            // Counted rather than flagged: the menu closes while what its item opened is still up.
            BeginBusy();
            flyout.Closed += Menu_Closed;
            flyout.ShowAt(this, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Menu_Closed(object sender, object e)
        {
            ((MenuFlyout)sender).Closed -= Menu_Closed;
            EndBusy();
        }

        private void PickMedia()
        {
            _ = PickFileAsync(Constants.MediaTypes, PickerLocationId.PicturesLibrary, false);
        }

        private void PickDocument()
        {
            _ = PickFileAsync(null, PickerLocationId.DocumentsLibrary, true);
        }

        private async void PickAudio()
        {
            if (ClientService == null)
            {
                return;
            }

            BeginBusy();

            try
            {
                var popup = new SendAudiosPopup(ClientService, NavigationService);

                var confirm = await popup.ShowAsync(XamlRoot);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                // None picked is the picker's way of asking for a file from this device.
                if (popup.SelectedItems is not { Count: > 0 } selected)
                {
                    await PickFileAsync(AudioTypes, PickerLocationId.MusicLibrary, false);
                }
                else if (selected[0].ToInputMessage() is InputMessageAudio audio)
                {
                    SetMedia(PollMediaDraft.FromAudio(audio.Audio), true);
                }
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task PickFileAsync(string[] types, PickerLocationId location, bool asDocument)
        {
            BeginBusy();

            try
            {
                var media = await PickSingleMediaAsync(types, location);
                if (media == null)
                {
                    return;
                }

                var draft = await PollMediaDraft.CreateAsync(media, Slot, asDocument);
                if (draft != null)
                {
                    SetMedia(draft, true);
                }
                else
                {
                    ToastPopup.Show(XamlRoot, Strings.UnsupportedAttachment, ToastPopupIcon.Info);
                }
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<StorageMedia> PickSingleMediaAsync(string[] types, PickerLocationId location)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = types == AudioTypes || types == null ? PickerViewMode.List : PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = location;

                if (types != null)
                {
                    picker.FileTypeFilter.AddRange(types);
                }
                else
                {
                    picker.FileTypeFilter.Add("*");
                }

                return await picker.PickSingleMediaAsync(XamlRoot);
            }
            catch
            {
                return null;
            }
        }

        private async void PickLocation()
        {
            if (ClientService == null)
            {
                return;
            }

            BeginBusy();

            try
            {
                var popup = new SendLocationPopup(ClientService.Session);

                // Over the popup this button may be in: one queued behind it would wait for it to close.
                var confirm = await popup.ShowAsync(XamlRoot);
                if (confirm == ContentDialogResult.Primary && PollMediaDraft.FromLocation(popup.Media) is PollMediaDraft draft)
                {
                    SetMedia(draft, true);
                }
            }
            finally
            {
                EndBusy();
            }
        }

        private async void PickLink()
        {
            BeginBusy();

            try
            {
                await PickLinkAsync();
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task PickLinkAsync()
        {
            var popup = new InputPopup
            {
                Title = Strings.PollV2AddLinkTitle,
                Header = Strings.PollV2AddLinkMessage,
                PlaceholderText = Strings.PollV2AddLinkUrlHint,
                PrimaryButtonText = Strings.Done,
                SecondaryButtonText = Strings.Cancel,
                InputScope = InputScopeNameValue.Url,
                Text = _media?.Input is InputPollMediaLink link ? link.Url : string.Empty
            };

            popup.Validating += LinkPopup_Validating;

            var confirm = await popup.ShowAsync(XamlRoot);

            popup.Validating -= LinkPopup_Validating;

            if (confirm == ContentDialogResult.Primary)
            {
                SetMedia(PollMediaDraft.FromLink(popup.Text.Trim()), true);
            }
        }

        private void LinkPopup_Validating(object sender, InputPopupValidatingEventArgs e)
        {
            e.Cancel = !e.Text.Trim().IsValidUrl();
        }

        private void PickSticker()
        {
            if (ClientService == null || _stickerFlyout != null)
            {
                return;
            }

            // Made for each use and dropped on close, as every option has a button.
            _stickerDrawer = new StickerDrawer
            {
                DataContext = StickerDrawerViewModel.Create(ClientService.Session),
                Width = 300,
                Height = 360
            };

            _stickerDrawer.ItemClick += StickerDrawer_ItemClick;
            _stickerDrawer.Activate(null);

            _stickerFlyout = new MenuFlyout();
            _stickerFlyout.MenuFlyoutPresenterStyle = new Style(typeof(MenuFlyoutPresenter));
            _stickerFlyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
            _stickerFlyout.Items.Add(new MenuFlyoutContent
            {
                Content = _stickerDrawer,
                Padding = new Thickness(0)
            });

            BeginBusy();
            _stickerFlyout.Closed += StickerFlyout_Closed;
            _stickerFlyout.ShowAt(this, FlyoutPlacementMode.Auto, FlyoutShowMode.Transient, source: this);
        }

        private void StickerDrawer_ItemClick(object sender, StickerDrawerItemClickEventArgs e)
        {
            SetMedia(PollMediaDraft.FromSticker(e.Sticker), true);
            _stickerFlyout?.Hide();
        }

        private void StickerFlyout_Closed(object sender, object e)
        {
            _stickerFlyout.Closed -= StickerFlyout_Closed;
            _stickerFlyout = null;

            // The view model stays subscribed to the aggregator until the drawer lets it go.
            _stickerDrawer.ItemClick -= StickerDrawer_ItemClick;
            _stickerDrawer.Deactivate();
            _stickerDrawer.DataContext = null;
            _stickerDrawer = null;

            EndBusy();
        }

        private void Remove()
        {
            SetMedia(null, true);
        }
    }
}
