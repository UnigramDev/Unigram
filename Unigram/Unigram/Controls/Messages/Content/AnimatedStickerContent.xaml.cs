using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class AnimatedStickerContent : HyperlinkButton, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public AnimatedStickerContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        private static readonly Dictionary<uint, uint> _thumbColor1 = new Dictionary<uint, uint>
        {
			{ 0xf77e41U, 0xca907aU },
			{ 0xffb139U, 0xedc5a5U },
			{ 0xffd140U, 0xf7e3c3U },
			{ 0xffdf79U, 0xfbefd6U },
	    };
        private static readonly Dictionary<uint, uint> _thumbColor2 = new Dictionary<uint, uint>
        {
			{ 0xf77e41U, 0xaa7c60U },
			{ 0xffb139U, 0xc8a987U },
			{ 0xffd140U, 0xddc89fU },
			{ 0xffdf79U, 0xe6d6b2U },
	    };
        private static readonly Dictionary<uint, uint> _thumbColor3 = new Dictionary<uint, uint>
        {
			{ 0xf77e41U, 0x8c6148U },
			{ 0xffb139U, 0xad8562U },
			{ 0xffd140U, 0xc49e76U },
			{ 0xffdf79U, 0xd4b188U },
	    };
        private static readonly Dictionary<uint, uint> _thumbColor4 = new Dictionary<uint, uint>
        {
			{ 0xf77e41U, 0x6e3c2cU },
			{ 0xffb139U, 0x925a34U },
			{ 0xffd140U, 0xa16e46U },
			{ 0xffdf79U, 0xac7a52U },
	    };
        private static readonly Dictionary<uint, uint> _thumbColor5 = new Dictionary<uint, uint>
        {
			{ 0xf77e41U, 0x291c12U },
			{ 0xffb139U, 0x472a22U },
			{ 0xffd140U, 0x573b30U },
			{ 0xffdf79U, 0x68493cU },
	    };

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var sticker = GetContent(message);
            if (sticker == null)
            {
                return;
            }

            //Background = null;
            //Texture.Source = null;
            //Texture.Constraint = message;
            if (message.GeneratedContent != null)
            {
                Width = Player.Width = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Height = Player.Height = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Player.ColorReplacements = null;

                if (message.Content is MessageText text && text.Text.Text.StartsWith("\uD83D\uDC4D"))
                {
                    switch (text.Text.Text.Last())
                    {
                        case '\uDFFB':
                            Player.ColorReplacements = _thumbColor1;
                            break;
                        case '\uDFFC':
                            Player.ColorReplacements = _thumbColor2;
                            break;
                        case '\uDFFD':
                            Player.ColorReplacements = _thumbColor3;
                            break;
                        case '\uDFFE':
                            Player.ColorReplacements = _thumbColor4;
                            break;
                        case '\uDFFF':
                            Player.ColorReplacements = _thumbColor5;
                            break;
                    }
                }
            }
            else
            {
                Width = Player.Width = 200;
                Height = Player.Height = 200;
                Player.ColorReplacements = null;
            }

            if (sticker.Thumbnail != null && !sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Thumbnail.Photo);
            }

            UpdateFile(message, sticker.StickerValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message);
            if (sticker == null)
            {
                return;
            }

            if (sticker.Thumbnail != null && sticker.Thumbnail.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (sticker.StickerValue.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                if (SettingsService.Current.Diagnostics.PlayStickers)
                {
                    LayoutRoot.Background = null;

                    Player.IsCachingEnabled = SettingsService.Current.Diagnostics.CacheStickers;
                    Player.IsLoopingEnabled = message.GeneratedContent == null && SettingsService.Current.Stickers.IsLoopingEnabled;
                    Player.Source = new Uri("file:///" + file.Local.Path);
                }
                else
                {
                    Player.Source = null;
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private async void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                LayoutRoot.Background = new ImageBrush { ImageSource = await PlaceholderHelper.GetWebpAsync(file.Local.Path) };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            // We can't recycle it as we must destroy CanvasAnimatedControl on Unload.
            return false;

            if (content is MessageSticker sticker)
            {
                return sticker.Sticker.IsAnimated;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.IsAnimated;
            }

            return false;
        }

        private Sticker GetContent(MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Sticker;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var sticker = GetContent(_message);
            if (sticker == null)
            {
                return;
            }

            if (_message.GeneratedContent != null)
            {
                Player.Play();
            }
            else
            {
                _message.Delegate.OpenSticker(sticker);
            }
        }
    }
}
