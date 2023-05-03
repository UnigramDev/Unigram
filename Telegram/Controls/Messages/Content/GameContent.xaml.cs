//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed class GameContent : Control, IContent, IContentWithPlayback
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public GameContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(GameContent);
        }

        #region InitializeComponent

        private TextBlock TitleLabel;
        private Span Span;
        private Border Media;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            Span = GetTemplateChild(nameof(Span)) as Span;
            Media = GetTemplateChild(nameof(Media)) as Border;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var game = message.Content as MessageGame;
            if (game == null || !_templateApplied)
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

        public void Recycle()
        {
            if (Media.Child is IContent content)
            {
                content.Recycle();
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageGame;
        }

        public IPlayerView GetPlaybackElement()
        {
            if (Media?.Child is IContentWithPlayback content)
            {
                return content.GetPlaybackElement();
            }
            else if (Media?.Child is IPlayerView playback)
            {
                return playback;
            }

            return null;
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
                else if (entity.Type is TextEntityTypeUrl or TextEntityTypeEmailAddress or TextEntityTypePhoneNumber or TextEntityTypeMention or TextEntityTypeHashtag or TextEntityTypeCashtag or TextEntityTypeBotCommand)
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
                else if (entity.Type is TextEntityTypeTextUrl or TextEntityTypeMentionName)
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
                _message.ClientService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive)
            {
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ClientService.DownloadFile(file.Id, 30);
            }
            else
            {
                _message.Delegate.OpenFile(file);
            }
        }
    }
}
