using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class GameContent : StackPanel, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public GameContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var game = message.Content as MessageGame;
            if (game == null)
            {
                return;
            }

            TitleLabel.Text = game.Game.Title;

            if (game.Game.Text == null || string.IsNullOrEmpty(game.Game.Text.Text))
            {
                Span.Inlines.Clear();
                Span.Inlines.Add(new Run { Text = game.Game.Description });
            }
            else
            {
                Span.Inlines.Clear();
                ReplaceEntities(Span, game.Game.Text);
            }

            UpdateContent(message, game.Game);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        private void UpdateContent(MessageViewModel message, Game game)
        {
            if (Media.Child is IContent content && content.IsValid(message.Content, false))
            {
                content.UpdateMessage(message);
            }
            else
            {
                if (game.Animation != null)
                {
                    Media.Child = new AnimationContent(message);
                }
                else if (game.Photo != null)
                {
                    // Photo at last: web page preview might have both a file and a thumbnail
                    Media.Child = new PhotoContent(message);
                }
                else
                {
                    Media.Child = null;
                }
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            if (Media.Child is IContentWithFile content && content.IsValid(message.Content, false))
            {
                content.UpdateFile(message, file);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageGame;
        }

        #region Entities

        private void ReplaceEntities(Span span, FormattedText text)
        {
            ReplaceEntities(span, text.Text, text.Entities);
        }

        private void ReplaceEntities(Span span, string text, IList<TextEntity> entities)
        {
            var previous = 0;

            foreach (var entity in entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.Type is TextEntityTypeBold)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = FontStyle.Italic });
                }
                else if (entity.Type is TextEntityTypeCode)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypePreCode)
                {
                    // TODO any additional
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypePhoneNumber || entity.Type is TextEntityTypeMention || entity.Type is TextEntityTypeHashtag || entity.Type is TextEntityTypeCashtag || entity.Type is TextEntityTypeBotCommand)
                {
                    var hyperlink = new Hyperlink();
                    var data = text.Substring(entity.Offset, entity.Length);

                    //hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, data);
                    hyperlink.Inlines.Add(new Run { Text = data });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);

                    if (entity.Type is TextEntityTypeUrl)
                    {
                        MessageHelper.SetEntityData(hyperlink, data);
                    }
                }
                else if (entity.Type is TextEntityTypeTextUrl || entity.Type is TextEntityTypeMentionName)
                {
                    var hyperlink = new Hyperlink();
                    object data;
                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        data = textUrl.Url;
                        MessageHelper.SetEntityData(hyperlink, textUrl.Url);
                        ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                    }
                    else if (entity.Type is TextEntityTypeMentionName mentionName)
                    {
                        data = mentionName.UserId;
                    }

                    //hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, null);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }
        }

        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var game = _message.Content as MessageGame;

            var file = game.Game.Animation?.AnimationValue ?? game.Game.Photo?.GetBig()?.Photo;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ProtoService.DownloadFile(file.Id, 32);
            }
            else
            {
                _message.Delegate.OpenFile(file);
            }
        }
    }
}
