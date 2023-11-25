//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ThemePreviewPopup : ContentPopup
    {
        private StorageFile _file;

        public ThemePreviewPopup(StorageFile file)
        {
            InitializeComponent();
            Initialize(file);
        }

        private async void Initialize(StorageFile file)
        {
            _file = file;
            var theme = await ThemeCustomInfo.FromFileAsync(file);

            ThemePreview.Update(theme);

            TitleLabel.Text = theme.Name;
            LayoutRoot.RequestedTheme = theme.Parent == TelegramTheme.Light
                ? ElementTheme.Light
                : ElementTheme.Dark;

            Chat1.Mockup(new ChatTypePrivate(), 0, "Eva Summer", string.Empty, "Reminds me of a Chinese proverb...", false, 0, false, true, DateTime.Now);
            Chat2.Mockup(new ChatTypePrivate(), 1, "Alexandra Smith", string.Empty, "This is amazing!", false, 2, false, false, DateTime.Now.AddHours(-1));
            Chat3.Mockup(new ChatTypePrivate(), 2, "Mike Apple", "😄 " + Strings.AttachSticker, string.Empty, false, 2, true, false, DateTime.Now.AddHours(-2), true);
            Chat4.Mockup(new ChatTypeSupergroup(), 3, "Evening Club", "Eva: " + Strings.AttachPhoto, string.Empty, false, 0, false, false, DateTime.Now.AddHours(-3));
            Chat5.Mockup(new ChatTypeSupergroup(), 4, "Old Pirates", "Max: ", "Yo-ho-ho!", false, 0, false, false, DateTime.Now.AddHours(-4));
            Chat6.Mockup(new ChatTypePrivate(), 5, "Max Bright", string.Empty, "How about some coffee?", true, 0, false, false, DateTime.Now.AddHours(-5));
            Chat7.Mockup(new ChatTypePrivate(), 6, "Natalie Parker", string.Empty, "OK, great)", true, 0, false, false, DateTime.Now.AddHours(-6));

            Photo.Source = PlaceholderImage.GetNameForUser(Strings.ThemePreviewTitle);
            Title.Text = Strings.ThemePreviewTitle;
            Subtitle.Text = string.Format("{0} {1} {2}", Strings.LastSeen, Strings.TodayAt, Formatter.ShortTime.Format(DateTime.Now.AddHours(-1)));

            Message1.Mockup(new MessagePhoto(new Photo(false, null, new[] { new PhotoSize("i", TdExtensions.GetLocalFile("Assets\\Mockup\\theme_preview_image.jpg"), 500, 302, Array.Empty<int>()) }), new FormattedText(), false, false), Strings.ThemePreviewLine4, false, DateTime.Now.AddSeconds(-25), true, true);
            Message2.Mockup(Strings.ThemePreviewLine1, true, DateTime.Now, true, false);
            //Message3.Mockup(Strings.FontSizePreviewLine1, Strings.FontSizePreviewName, Strings.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
            Message3.Mockup(new MessageVoiceNote(new VoiceNote(3, new byte[]
            {
                0, 0, 163, 198, 43, 17, 250, 248, 127, 155, 85, 58, 159, 230, 164, 212, 185, 247, 73, 42,
                173, 66, 165, 69, 41, 251, 255, 242, 127, 223, 113, 133, 237, 148, 243, 30, 127, 184, 206, 183, 234,
                108, 175, 168, 250, 207, 114, 229, 233, 154, 35, 254, 21, 66, 99, 134, 141, 92, 159, 2
            }, "audio/ogg", null, null), new FormattedText(), true), true, DateTime.Now.AddSeconds(-25), false, true);
            Message4.Mockup(Strings.ThemePreviewLine3, Strings.ThemePreviewLine3Reply, Strings.ThemePreviewLine1, false, DateTime.Now.AddSeconds(-25), true, false);
            Message5.Mockup(new MessageAudio(new Audio(4 * 60 + 3, Strings.ThemePreviewSongTitle, Strings.ThemePreviewSongPerformer, "preview.mp3", "audio/mp3", null, null, null, null), new FormattedText()), false, DateTime.Now, false, true);
            Message6.Mockup(Strings.ThemePreviewLine2, true, DateTime.Now, true, true);

            PrimaryButtonText = Strings.ApplyTheme;
            SecondaryButtonText = Strings.Cancel;
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                await TypeResolver.Current.Resolve<IThemeService>().InstallThemeAsync(_file);
            }
            catch { }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
