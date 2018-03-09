using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Telegram.Helpers;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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
    public sealed partial class ContactContent : Grid, IContent
    {
        private MessageViewModel _message;

        public ContactContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var contact = message.Content as MessageContact;
            if (contact == null)
            {
                return;
            }

            var user = message.ProtoService.GetUser(contact.Contact.UserId);
            if (user != null)
            {
                Photo.Source = PlaceholderHelper.GetUser(message.ProtoService, user, 48, 48);

                Title.Text = user.GetFullName();
                Subtitle.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);
            }
            else
            {
                var fullName = string.IsNullOrEmpty(contact.Contact.LastName) ? contact.Contact.FirstName : $"{contact.Contact.FirstName} {contact.Contact.LastName}";

                Photo.Source = PlaceholderHelper.GetBadge(fullName, Colors.Red, 48, 48);

                Title.Text = fullName;
                Subtitle.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageContact;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var contact = _message.Content as MessageContact;
            if (contact == null)
            {
                return;
            }

            _message.Delegate.OpenUser(contact.Contact.UserId);
        }
    }
}
