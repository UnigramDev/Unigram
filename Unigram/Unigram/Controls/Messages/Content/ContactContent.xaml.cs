using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class ContactContent : Grid, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

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
                Photo.Source = PlaceholderHelper.GetUser(message.ProtoService, user, 48);

                Title.Text = user.GetFullName();
                Subtitle.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);
            }
            else
            {
                Photo.Source = PlaceholderHelper.GetNameForUser(contact.Contact.FirstName, contact.Contact.LastName, 48);

                Title.Text = contact.Contact.GetFullName();
                Subtitle.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);
            }

            Button.Visibility = string.IsNullOrEmpty(contact.Contact.Vcard) ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageContact;
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            var contact = _message.Content as MessageContact;
            if (contact == null)
            {
                return;
            }

            _message.Delegate.OpenUser(contact.Contact.UserId);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var contact = _message.Content as MessageContact;
            if (contact == null)
            {
                return;
            }

            try
            {
                var stream = new InMemoryRandomAccessStream();
                var writer = new DataWriter(stream.GetOutputStreamAt(0));

                var reference = RandomAccessStreamReference.CreateFromStream(stream);

                writer.WriteString(contact.Contact.Vcard);
                await writer.StoreAsync();

                var system = await Windows.ApplicationModel.Contacts.ContactManager.ConvertVCardToContactAsync(reference);

                var transform = this.TransformToVisual(Window.Current.Content as UIElement);
                var point = transform.TransformPoint(new Point());

                Windows.ApplicationModel.Contacts.ContactManager.ShowContactCard(system, new Rect(point.X, point.Y, ActualWidth, ActualHeight));
            }
            catch { }
        }
    }
}
