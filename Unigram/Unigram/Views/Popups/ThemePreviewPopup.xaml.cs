using System;
using System.Globalization;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

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

            var flags = TelegramTheme.Light;
            var theme = SettingsService.Current.Appearance.RequestedTheme;
            var mapping = TLContainer.Current.Resolve<IThemeService>().GetMapping(flags);

            var file = await StorageFile.GetFileFromPathAsync(path);
            var lines = await FileIO.ReadLinesAsync(file);
            var dict = new ResourceDictionary();

            foreach (var line in lines)
            {
                if (line.StartsWith("name: "))
                {
                    TitleLabel.Text = line.Substring("name: ".Length);
                }
                else if (line.StartsWith("parent: "))
                {
                    flags = (TelegramTheme)int.Parse(line.Substring("parent: ".Length));
                    mapping = TLContainer.Current.Resolve<IThemeService>().GetMapping(flags);

                    dict["MessageForegroundBrush"] = new SolidColorBrush(TLContainer.Current.Resolve<IThemeService>().GetDefaultColor(flags, "MessageForegroundColor"));
                }
                else if (line.Equals("!") || line.Equals("#"))
                {
                    continue;
                }
                else
                {
                    var split = line.Split(':');
                    var key = split[0].Trim();
                    var value = split[1].Trim();

                    if (value.StartsWith("#") && int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
                    {
                        byte a = (byte)((hexValue & 0xff000000) >> 24);
                        byte r = (byte)((hexValue & 0x00ff0000) >> 16);
                        byte g = (byte)((hexValue & 0x0000ff00) >> 8);
                        byte b = (byte)(hexValue & 0x000000ff);

                        if (key.EndsWith("Brush"))
                        {
                            dict[key] = new SolidColorBrush(Color.FromArgb(a, r, g, b));

                            if (mapping.TryGetValue(key, out string[] additional))
                            {
                                foreach (var child in additional)
                                {
                                    dict[child] = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                                }
                            }
                        }
                        else if (key.EndsWith("Color"))
                        {
                            dict[key] = Color.FromArgb(a, r, g, b);
                            dict[key.Substring(0, key.Length - 5) + "Brush"] = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                        }
                    }
                }
            }

            LayoutRoot.Resources.ThemeDictionaries[flags == TelegramTheme.Light ? "Light" : "Dark"] = dict;
            LayoutRoot.RequestedTheme = flags == TelegramTheme.Light ? ElementTheme.Light : ElementTheme.Dark;

            Chat1.Mockup(new ChatTypePrivate(), 0, "Eva Summer", string.Empty, "Reminds me of a Chinese proverb...", false, 0, false, true, DateTime.Now);
            Chat2.Mockup(new ChatTypePrivate(), 1, "Alexandra Smith", string.Empty, "This is amazing!", false, 2, false, false, DateTime.Now.AddHours(-1));
            Chat3.Mockup(new ChatTypePrivate(), 2, "Mike Apple", "😄 " + Strings.Resources.AttachSticker, string.Empty, false, 2, true, false, DateTime.Now.AddHours(-2), true);
            Chat4.Mockup(new ChatTypeSupergroup(), 3, "Evening Club", "Eva: " + Strings.Resources.AttachPhoto, string.Empty, false, 0, false, false, DateTime.Now.AddHours(-3));
            Chat5.Mockup(new ChatTypeSupergroup(), 4, "Old Pirates", "Max: ", "Yo-ho-ho!", false, 0, false, false, DateTime.Now.AddHours(-4));
            Chat6.Mockup(new ChatTypePrivate(), 5, "Max Bright", string.Empty, "How about some coffee?", true, 0, false, false, DateTime.Now.AddHours(-5));
            Chat7.Mockup(new ChatTypePrivate(), 6, "Natalie Parker", string.Empty, "OK, great)", true, 0, false, false, DateTime.Now.AddHours(-6));

            Photo.Source = PlaceholderHelper.GetNameForUser("Reinhardt", 30);
            Title.Text = "Reinhardt";
            Subtitle.Text = string.Format("{0} {1} {2}", Strings.Resources.LastSeen, Strings.Resources.TodayAt, BindConvert.Current.ShortTime.Format(DateTime.Now.AddHours(-1)));

            Message1.Mockup(new MessagePhoto(new Photo(false, null, new[] { new PhotoSize("i", new File { Local = new LocalFile { Path = "ms-appx:///Assets/Mockup/theme_preview_image.jpg" } }, 500, 302, new int[0]) }), new FormattedText(), false), "Bring it on! I LIVE for this!", false, DateTime.Now.AddSeconds(-25), true, true);
            Message2.Mockup("Reinhardt, we need to find you some new tunes 🎶.", true, DateTime.Now, true, false);
            //Message3.Mockup(Strings.Resources.FontSizePreviewLine1, Strings.Resources.FontSizePreviewName, Strings.Resources.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
            Message3.Mockup(new MessageVoiceNote(new VoiceNote(3, new byte[]
            {
                0, 0, 163, 198, 43, 17, 250, 248, 127, 155, 85, 58, 159, 230, 164, 212, 185, 247, 73, 42,
                173, 66, 165, 69, 41, 251, 255, 242, 127, 223, 113, 133, 237, 148, 243, 30, 127, 184, 206, 183, 234,
                108, 175, 168, 250, 207, 114, 229, 233, 154, 35, 254, 21, 66, 99, 134, 141, 92, 159, 2
            }, "audio/ogg", null), new FormattedText(), true), true, DateTime.Now.AddSeconds(-25), false, true);
            Message4.Mockup("Ah, you kids today with techno music! You should enjoy the classics, like Hasselhoff!", "Lucio", "Reinhardt, we need to find you some new tunes 🎶.", false, DateTime.Now.AddSeconds(-25), true, false);
            Message5.Mockup(new MessageAudio(new Audio(4 * 60 + 3, "True Survivor", "David Hasselhoff", "preview.mp3", "audio/mp3", null, null, null), new FormattedText()), false, DateTime.Now, false, true);
            Message6.Mockup("I can't even take you seriously right now.", true, DateTime.Now, true, true);

            PrimaryButtonText = Strings.Resources.ApplyTheme;
            SecondaryButtonText = Strings.Resources.Cancel;

            var shadow = DropShadowEx.Attach(Shadow, 20, 0.25f);
            shadow.RelativeSizeAdjustment = Vector2.One;
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
