//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls.Messages.Content
{
    public sealed class ContactContent : Windows.UI.Xaml.Controls.Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public ContactContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(ContactContent);
        }

        #region InitializeComponent

        private ProfilePicture Photo;
        private TextBlock Title;
        private TextBlock Subtitle;
        private Button Button;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Button = GetTemplateChild(nameof(Button)) as Button;

            Photo.Click += Photo_Click;
            Button.Click += Button_Click;

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

            var contact = message.Content as MessageContact;
            if (contact == null || !_templateApplied)
            {
                return;
            }

            if (message.ClientService.TryGetUser(contact.Contact.UserId, out User user))
            {
                Photo.SetUser(message.ClientService, user, 48);
            }
            else
            {
                Photo.Source = PlaceholderHelper.GetNameForUser(contact.Contact.FirstName, contact.Contact.LastName, 48);
            }

            Title.Text = contact.Contact.GetFullName();
            Subtitle.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);

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

                var transform = TransformToVisual(Window.Current.Content);
                var point = transform.TransformPoint(new Point());

                Windows.ApplicationModel.Contacts.ContactManager.ShowContactCard(system, new Rect(point.X, point.Y, ActualWidth, ActualHeight));
            }
            catch { }
        }
    }
}
