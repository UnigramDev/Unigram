using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Unigram.Common;
using Unigram.Converters;
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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class AnimationContent : AspectView, IContentWithFile
    {
        private MessageViewModel _message;

        public AnimationContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var animation = GetContent(message.Content);
            if (animation == null)
            {
                return;
            }

            Constraint = message;
            Texture.Source = null;

            if (animation.Thumbnail != null)
            {
                UpdateThumbnail(message, animation.Thumbnail.Photo);
            }

            UpdateFile(message, animation.AnimationData);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.Ttl > 0)
            {
                //Timer.Maximum = message.Ttl;
                //Timer.Value = DateTime.Now.AddSeconds(message.TtlExpiresIn);
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var animation = GetContent(message.Content);
            if (animation == null)
            {
                return;
            }

            if (animation.Thumbnail != null && animation.Thumbnail.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (animation.AnimationData.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                Overlay.Opacity = 1;
            }
            else if (file.Remote.IsUploadingActive)
            {

                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
                Overlay.Opacity = 1;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.Glyph = "\uE118";
                Button.Progress = 0;

                Subtitle.Text = Strings.Android.AttachGif + ", " + FileSizeConverter.Convert(size);
                Overlay.Opacity = 1;

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.Send(new DownloadFile(file.Id, 32));
                }
            }
            else
            {
                Button.Glyph = "\uE906";
                Button.Progress = 1;

                Overlay.Opacity = 0;
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                Texture.Source = PlaceholderHelper.GetBlurred(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageAnimation)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Animation != null;
            }

            return false;
        }

        private Animation GetContent(MessageContent content)
        {
            if (content is MessageAnimation animation)
            {
                return animation.Animation;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Animation;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var animation = GetContent(_message.Content);
            if (animation == null)
            {
                return;
            }

            var file = animation.AnimationData;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Remote.IsUploadingActive)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ProtoService.Send(new DownloadFile(file.Id, 32));
            }
            else
            {
                _message.Delegate.OpenMedia(_message, this);
            }
        }
    }
}
