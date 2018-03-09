using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class VoiceNoteContent : Grid, IContentWithFile
    {
        private MessageViewModel _message;

        public VoiceNoteContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            Progress.UpdateWave(voiceNote);

            if (message.Content is MessageVoiceNote voiceNoteMessage)
            {
                Subtitle.Text = voiceNote.GetDuration() + (voiceNoteMessage.IsListened ? string.Empty : " ●");
            }
            else
            {
                Subtitle.Text = voiceNote.GetDuration();
            }

            UpdateFile(message, voiceNote.Voice);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.Content is MessageVoiceNote voiceNote)
            {
                Subtitle.Text = voiceNote.VoiceNote.GetDuration() + (voiceNote.IsListened ? string.Empty : " ●");
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            else if (voiceNote.Voice.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Local.DownloadedSize / size;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {

                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Remote.UploadedSize / size;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.Glyph = "\uE118";
                Button.Progress = 0;

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.Send(new DownloadFile(file.Id, 32));
                }
            }
            else
            {
                Button.Glyph = "\uE102";
                Button.Progress = 1;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageVoiceNote)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.VoiceNote != null;
            }

            return false;
        }

        private VoiceNote GetContent(MessageContent content)
        {
            if (content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.VoiceNote;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var voiceNote = GetContent(_message.Content);
            if (voiceNote == null)
            {
                return;
            }

            var file = voiceNote.Voice;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
            else
            {
                _message.Delegate.PlayMessage(_message);
            }
        }
    }
}
