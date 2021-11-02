using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Storage;

namespace Unigram.Views.Popups
{
    public sealed partial class ThemePreviewPopup : ContentPopup
    {
        private string _path;

        public ThemePreviewPopup(string path)
        {
            InitializeComponent();
            Initialize(path);
        }

        private async void Initialize(string path)
        {
            _path = path;
            ThemePreview.Initialize(path);

            var flags = TelegramTheme.Light;

            var file = await StorageFile.GetFileFromPathAsync(path);
            var lines = await FileIO.ReadLinesAsync(file);

            foreach (var line in lines)
            {
                if (line.StartsWith("name: "))
                {
                    TitleLabel.Text = line.Substring("name: ".Length);
                }
                else if (line.StartsWith("parent: "))
                {
                    flags = (TelegramTheme)int.Parse(line.Substring("parent: ".Length));
                }
            }

            LayoutRoot.RequestedTheme = flags == TelegramTheme.Light ? ElementTheme.Light : ElementTheme.Dark;

            Chat1.Mockup(new ChatTypePrivate(), 0, "Eva Summer", string.Empty, "Reminds me of a Chinese proverb...", false, 0, false, true, DateTime.Now);
            Chat2.Mockup(new ChatTypePrivate(), 1, "Alexandra Smith", string.Empty, "This is amazing!", false, 2, false, false, DateTime.Now.AddHours(-1));
            Chat3.Mockup(new ChatTypePrivate(), 2, "Mike Apple", "😄 " + Strings.Resources.AttachSticker, string.Empty, false, 2, true, false, DateTime.Now.AddHours(-2), true);
            Chat4.Mockup(new ChatTypeSupergroup(), 3, "Evening Club", "Eva: " + Strings.Resources.AttachPhoto, string.Empty, false, 0, false, false, DateTime.Now.AddHours(-3));
            Chat5.Mockup(new ChatTypeSupergroup(), 4, "Old Pirates", "Max: ", "Yo-ho-ho!", false, 0, false, false, DateTime.Now.AddHours(-4));
            Chat6.Mockup(new ChatTypePrivate(), 5, "Max Bright", string.Empty, "How about some coffee?", true, 0, false, false, DateTime.Now.AddHours(-5));
            Chat7.Mockup(new ChatTypePrivate(), 6, "Natalie Parker", string.Empty, "OK, great)", true, 0, false, false, DateTime.Now.AddHours(-6));

            Photo.Source = PlaceholderHelper.GetNameForUser(Strings.Resources.ThemePreviewTitle, 30);
            Title.Text = Strings.Resources.ThemePreviewTitle;
            Subtitle.Text = string.Format("{0} {1} {2}", Strings.Resources.LastSeen, Strings.Resources.TodayAt, Converter.ShortTime.Format(DateTime.Now.AddHours(-1)));

            Message1.Mockup(new MessagePhoto(new Photo(false, null, new[] { new PhotoSize("i", new File { Local = new LocalFile { Path = "ms-appx:///Assets/Mockup/theme_preview_image.jpg", IsDownloadingCompleted = true } }, 500, 302, new int[0]) }), new FormattedText(), false), Strings.Resources.ThemePreviewLine4, false, DateTime.Now.AddSeconds(-25), true, true);
            Message2.Mockup(Strings.Resources.ThemePreviewLine1, true, DateTime.Now, true, false);
            //Message3.Mockup(Strings.Resources.FontSizePreviewLine1, Strings.Resources.FontSizePreviewName, Strings.Resources.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
            Message3.Mockup(new MessageVoiceNote(new VoiceNote(3, new byte[]
            {
                0, 0, 163, 198, 43, 17, 250, 248, 127, 155, 85, 58, 159, 230, 164, 212, 185, 247, 73, 42,
                173, 66, 165, 69, 41, 251, 255, 242, 127, 223, 113, 133, 237, 148, 243, 30, 127, 184, 206, 183, 234,
                108, 175, 168, 250, 207, 114, 229, 233, 154, 35, 254, 21, 66, 99, 134, 141, 92, 159, 2
            }, "audio/ogg", null), new FormattedText(), true), true, DateTime.Now.AddSeconds(-25), false, true);
            Message4.Mockup(Strings.Resources.ThemePreviewLine3, Strings.Resources.ThemePreviewLine3Reply, Strings.Resources.ThemePreviewLine1, false, DateTime.Now.AddSeconds(-25), true, false);
            Message5.Mockup(new MessageAudio(new Audio(4 * 60 + 3, Strings.Resources.ThemePreviewSongTitle, Strings.Resources.ThemePreviewSongPerformer, "preview.mp3", "audio/mp3", null, null, null), new FormattedText()), false, DateTime.Now, false, true);
            Message6.Mockup(Strings.Resources.ThemePreviewLine2, true, DateTime.Now, true, true);

            PrimaryButtonText = Strings.Resources.ApplyTheme;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(_path);
                await TLContainer.Current.Resolve<IThemeService>().InstallThemeAsync(file);
            }
            catch { }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
