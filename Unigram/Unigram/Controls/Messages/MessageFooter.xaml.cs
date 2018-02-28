using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Messages
{
    public sealed partial class MessageFooter : ContentPresenter
    {
        private MessageViewModel _mesage;

        public MessageFooter()
        {
            InitializeComponent();
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _mesage = message;

            ConvertState(message);
            ConvertDate(message);
            ConvertEdited(message);
            ConvertViews(message);
        }

        public void UpdateMessageState(MessageViewModel message)
        {
            ConvertState(message);
        }

        public void UpdateMessageEdited(MessageViewModel message)
        {
            ConvertEdited(message);
        }

        public void UpdateMessageViews(MessageViewModel message)
        {
            ConvertViews(message);
        }

        public void ConvertDate(MessageViewModel message)
        {
            DateLabel.Text = BindConvert.Current.Date(message.Date);
        }

        public void Mockup(bool outgoing, DateTime date)
        {
            DateLabel.Text = BindConvert.Current.ShortTime.Format(date);
            StateLabel.Text = outgoing ? "\u00A0\u00A0\uE601" : string.Empty;
        }

        public void ConvertViews(MessageViewModel message)
        {
            if (message.Views > 0)
            {
                //ViewsGlyph.Text = "\uE607\u2009";

                var number = BindConvert.Current.ShortNumber(Math.Max(message.Views, 1));
                number += "   ";

                if (message.IsChannelPost && !string.IsNullOrEmpty(message.AuthorSignature))
                {
                    number += $"{message.AuthorSignature}, ";
                }
                else if (message.ForwardInfo is MessageForwardedPost forwardedPost && !string.IsNullOrEmpty(forwardedPost.AuthorSignature))
                {
                    number += $"{forwardedPost.AuthorSignature}, ";
                }

                ViewsGlyph.Text = "\uE607\u00A0\u00A0";
                ViewsLabel.Text = number;
            }
            else
            {
                ViewsGlyph.Text = string.Empty;
                ViewsLabel.Text = string.Empty;
            }
        }

        private void ConvertEdited(MessageViewModel message)
        {
            //var message = ViewModel;
            //var bot = false;
            //if (message.From != null)
            //{
            //    bot = message.From.IsBot;
            //}

            var bot = false;

            var sender = message.GetSenderUser();
            if (sender != null && sender.Type is UserTypeBot)
            {
                bot = true;
            }

            EditedLabel.Text = message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && !(message.ReplyMarkup is ReplyMarkupInlineKeyboard) ? $"{Strings.Resources.EditedMessage}\u00A0\u2009" : string.Empty;
        }

        private void ConvertState(MessageViewModel message)
        {
            if (message.IsOutgoing && !message.IsChannelPost && !message.IsSaved())
            {
                var maxId = 0L;

                var chat = message.GetChat();
                if (chat != null)
                {
                    maxId = chat.LastReadOutboxMessageId;
                }

                if (message.SendingState is MessageSendingStateFailed)
                {
                    StateLabel.Text = "\u00A0\u00A0\uE611";
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    StateLabel.Text = "\u00A0\u00A0\uE600";
                }
                else if (message.Id <= maxId)
                {
                    StateLabel.Text = "\u00A0\u00A0\uE601";
                }
                else
                {
                    StateLabel.Text = "\u00A0\u00A0\uE602";
                }
            }
            else
            {
                StateLabel.Text = string.Empty;
            }
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var message = _mesage;
            if (message == null)
            {
                return;
            }

            var tooltip = sender as ToolTip;
            if (tooltip == null)
            {
                return;
            }

            var dateTime = BindConvert.Current.DateTime(message.Date);
            var date = BindConvert.Current.LongDate.Format(dateTime);
            var time = BindConvert.Current.LongTime.Format(dateTime);

            var text = $"{date} {time}";

            var bot = false;
            var user = message.GetSenderUser();
            if (user != null)
            {
                bot = user.Type is UserTypeBot;
            }

            if (message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && !(message.ReplyMarkup is ReplyMarkupInlineKeyboard))
            {
                var edit = BindConvert.Current.DateTime(message.EditDate);
                var editDate = BindConvert.Current.LongDate.Format(edit);
                var editTime = BindConvert.Current.LongTime.Format(edit);

                text += $"\r\n{Strings.Resources.EditedMessage}: {editDate} {editTime}";
            }

            if (message.ForwardInfo is MessageForwardedPost forwardedPost)
            {
                var original = BindConvert.Current.DateTime(forwardedPost.Date);
                var originalDate = BindConvert.Current.LongDate.Format(original);
                var originalTime = BindConvert.Current.LongTime.Format(original);

                text += $"\r\n{Strings.Additional.OriginalMessage}: {originalDate} {originalTime}";
            }
            else if (message.ForwardInfo is MessageForwardedFromUser forwardedFromUser)
            {
                var original = BindConvert.Current.DateTime(forwardedFromUser.Date);
                var originalDate = BindConvert.Current.LongDate.Format(original);
                var originalTime = BindConvert.Current.LongTime.Format(original);

                text += $"\r\n{Strings.Additional.OriginalMessage}: {originalDate} {originalTime}";
            }

            tooltip.Content = text;
        }
    }
}
