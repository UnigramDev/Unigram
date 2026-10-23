//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.Graphics.Canvas.Geometry;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Entities;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Stories.Popups
{
    /// <summary>
    /// A story link's address, its text, and whether it shows the page's preview, for a new link
    /// or one being edited.
    /// </summary>
    public sealed partial class StoryLinkPopup : ModalPopup
    {
        private const string Scheme = "https://";

        // What the other clients take for a link.
        private static readonly Regex UrlPattern = new("((https?)://|(www|ftp)\\.)?[a-z0-9-]+(\\.[a-z0-9-]+)+([/?]?.+)");

        private static readonly Color DefaultAccent = Color.FromArgb(0xFF, 0x33, 0x91, 0xD4);

        private readonly IClientService _clientService;
        private readonly DispatcherTimer _previewTimer;

        private LinkPreview _preview;
        private bool _previewLoading;
        private string _previewUrl;

        private bool _textAbove = true;
        private bool _largePhoto;

        public StoryLinkPopup(IClientService clientService, PaintEntity link)
        {
            InitializeComponent();

            _clientService = clientService;

            Title = Strings.StoryLinkCreate;
            PrimaryButtonText = link == null ? Strings.StoryLinkAdd : Strings.StoryLinkEdit;
            CloseButtonText = Strings.Cancel;

            // The preview is asked for once typing stops for a moment.
            _previewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(700)
            };

            _previewTimer.Tick += PreviewTimer_Tick;

            if (link != null)
            {
                _textAbove = link.LinkTextAbove;
                _largePhoto = link.LinkLargePhoto;

                NameCheck.IsChecked = !string.IsNullOrEmpty(link.LinkName);
                NameField.Text = link.LinkName ?? string.Empty;
                UrlField.Text = link.LinkUrl ?? string.Empty;
            }
            else
            {
                UrlField.Text = Scheme;
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            UrlField.Focus(FocusState.Keyboard);
            UrlField.SelectionStart = UrlField.Text.Length;

            UpdatePaste();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;

            _previewTimer.Stop();
            _previewTimer.Tick -= PreviewTimer_Tick;
        }

        private static bool ContainsUrl(string text)
        {
            return !string.IsNullOrEmpty(text) && UrlPattern.IsMatch(text);
        }

        private void UrlField_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = UrlField.Text;

            // A link pasted after the scheme already in the field brings its own.
            if (text.StartsWith(Scheme + "http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith(Scheme + Scheme, StringComparison.OrdinalIgnoreCase))
            {
                UrlField.Text = text.Substring(Scheme.Length);
                UrlField.SelectionStart = UrlField.Text.Length;
                return;
            }

            UpdatePaste();

            var valid = ContainsUrl(text);
            IsPrimaryButtonEnabled = valid;

            if (text == _previewUrl)
            {
                return;
            }

            _previewUrl = text;
            _previewTimer.Stop();

            if (valid)
            {
                _previewTimer.Start();
                ShowPreview(null, true);
            }
            else
            {
                ShowPreview(null, false);
            }
        }

        private async void PreviewTimer_Tick(object sender, object e)
        {
            _previewTimer.Stop();

            var url = _previewUrl;

            var response = await _clientService.SendAsync(new GetLinkPreview(new FormattedText(url, Array.Empty<TextEntity>()), null));

            // Typing went on while it was asked for.
            if (url != _previewUrl)
            {
                return;
            }

            if (response is LinkPreview preview && (preview.HasText() || preview.HasPhoto()))
            {
                ShowPreview(preview, false);
            }
            else
            {
                ShowPreview(null, false);
            }
        }

        private void ShowPreview(LinkPreview preview, bool loading)
        {
            _preview = preview;
            _previewLoading = loading;

            PreviewRoot.Visibility = preview != null || loading
                ? Visibility.Visible
                : Visibility.Collapsed;

            PreviewRoot.IsEnabled = preview != null;
            PreviewContent.Visibility = preview != null
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (loading)
            {
                PreviewSkeleton.Visibility = Visibility.Visible;
                VisualUtilities.SetSkeleton(PreviewSkeleton, new Vector2(240, 52),
                    CanvasGeometry.CreateRoundedRectangle(null, 0, 4, 120, 12, 4, 4),
                    CanvasGeometry.CreateRoundedRectangle(null, 0, 22, 220, 12, 4, 4),
                    CanvasGeometry.CreateRoundedRectangle(null, 0, 40, 160, 10, 4, 4));
            }
            else
            {
                VisualUtilities.HideSkeleton(PreviewSkeleton);
                PreviewSkeleton.Visibility = Visibility.Collapsed;
            }

            if (preview == null)
            {
                return;
            }

            SetText(PreviewSiteName, preview.SiteName);
            SetText(PreviewTitle, string.IsNullOrEmpty(preview.Title) ? preview.Author : preview.Title);
            SetText(PreviewDescription, preview.Description?.Text);
        }

        private static void SetText(TextBlock block, string text)
        {
            block.Text = text ?? string.Empty;
            block.Visibility = string.IsNullOrEmpty(text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (_preview == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ToggleTextAbove, _textAbove ? Strings.LinkBelow : Strings.LinkAbove, _textAbove ? Icons.MoveDown : Icons.MoveUp);

            if (_preview.HasPhoto())
            {
                flyout.CreateFlyoutItem(ToggleLargePhoto, _largePhoto ? Strings.LinkMediaSmaller : Strings.LinkMediaLarger, _largePhoto ? Icons.Shrink : Icons.Enlarge);
            }

            flyout.CreateFlyoutItem(RemovePreview, Strings.DoNotLinkPreview, Icons.DismissCircle, destructive: true);

            flyout.ShowAt(PreviewRoot);
        }

        private void ToggleTextAbove()
        {
            _textAbove = !_textAbove;
        }

        private void ToggleLargePhoto()
        {
            _largePhoto = !_largePhoto;
        }

        // Until the link changes: a new one is asked for its own preview.
        private void RemovePreview()
        {
            ShowPreview(null, false);
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            var content = ClipboardEx.TryGetContent();
            if (content == null || !content.Contains(StandardDataFormats.Text))
            {
                return;
            }

            try
            {
                var text = await content.GetTextAsync();

                UrlField.Text = text.Trim();
                UrlField.SelectAll();
                UrlField.Focus(FocusState.Keyboard);
            }
            catch
            {
                // The clipboard changed hands meanwhile.
            }
        }

        // Offered while the field holds nothing of its own yet.
        private void UpdatePaste()
        {
            var empty = string.IsNullOrEmpty(UrlField.Text) || UrlField.Text == Scheme;
            var content = empty ? ClipboardEx.TryGetContent() : null;

            PasteButton.Visibility = content != null && content.Contains(StandardDataFormats.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void NameCheck_Toggled(object sender, RoutedEventArgs e)
        {
            var open = NameCheck.IsChecked == true;

            NameField.Visibility = open
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (open)
            {
                NameField.Focus(FocusState.Keyboard);
            }
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && ContainsUrl(UrlField.Text))
            {
                e.Handled = true;
                Hide(ContentDialogResult.Primary);
            }
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (!ContainsUrl(UrlField.Text))
            {
                VisualUtilities.ShakeView(UrlField);
                args.Cancel = true;
            }
        }

        /// <summary>
        /// Writes what was chosen into <paramref name="link"/>, together with the preview the card
        /// will show, so that drawing it later needs nothing fetched.
        /// </summary>
        public void ApplyTo(PaintEntity link)
        {
            var preview = _previewLoading ? null : _preview;

            link.LinkUrl = UrlField.Text.Trim();
            link.LinkName = NameCheck.IsChecked == true && !string.IsNullOrWhiteSpace(NameField.Text)
                ? NameField.Text.Trim()
                : null;

            link.LinkHasPreview = preview != null;
            link.LinkTextAbove = _textAbove;
            link.LinkLargePhoto = _largePhoto;

            link.LinkSiteName = preview?.SiteName;
            link.LinkTitle = string.IsNullOrEmpty(preview?.Title) ? preview?.Author : preview.Title;
            link.LinkDescription = preview?.Description?.Text;

            File file = null;
            int width = 0, height = 0;

            if (preview != null && preview.HasPhoto())
            {
                GetPhoto(preview, out file, out width, out height);
            }

            link.LinkPhotoFile = file;
            link.LinkPhotoRemoteId = file?.Remote.Id;
            link.LinkPhotoWidth = width;
            link.LinkPhotoHeight = height;

            link.LinkAccentColor = GetAccentColor();
        }

        // The largest size of a photo, which a large card shows across its width; otherwise the
        // thumbnail.
        private static void GetPhoto(LinkPreview preview, out File file, out int width, out int height)
        {
            var photo = preview.Type switch
            {
                LinkPreviewTypePhoto type => type.Photo,
                LinkPreviewTypeArticle type => type.Photo,
                LinkPreviewTypeApp type => type.Photo,
                LinkPreviewTypeWebApp type => type.Photo,
                _ => null
            };

            if (photo?.GetBig() is PhotoSize big)
            {
                file = big.Photo;
                width = big.Width;
                height = big.Height;
                return;
            }

            var thumbnail = preview.GetThumbnail();

            file = thumbnail?.File;
            width = thumbnail?.Width ?? 0;
            height = thumbnail?.Height ?? 0;
        }

        // The poster's own name colour.
        private Color GetAccentColor()
        {
            if (_clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                var colors = _clientService.GetAccentColor(user.AccentColorId)?.LightThemeColors;
                if (colors?.Count > 0)
                {
                    return colors[0];
                }
            }

            return DefaultAccent;
        }
    }
}
