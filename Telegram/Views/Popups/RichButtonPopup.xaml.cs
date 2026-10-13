//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.RichEditor;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public enum RichButtonPopupResult
    {
        Cancel,
        Save,
        Delete,
    }

    /// <summary>
    /// A button in a rich message, laid out as TDesktop's EditRichButtonBox: its label, where it
    /// goes, its colour shown as the buttons it can be, and what it does. Regular users may only
    /// make the three kinds offered here - a link, text to copy, a user's profile - which is what
    /// TDLib documents for <c>inlineButton.type</c>.
    /// </summary>
    public sealed partial class RichButtonPopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private long _userId;
        private RadioButton _previousType;
        private bool _reverting;
        private RichButtonPopupResult _result;

        /// <param name="separateLine">
        /// Whether the button goes in a row of its own, offered as a choice; null where there is
        /// no choice - a button being edited, or one going where only text can.
        /// </param>
        public RichButtonPopup(IClientService clientService, INavigationService navigationService, InlineButton button, bool canDelete, bool? separateLine = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            Title = button == null ? Strings.RichEditorAddButton : Strings.RichEditorEditButton;
            PrimaryButtonContent = Strings.Save;
            SecondaryButtonContent = Strings.Cancel;

            DeleteButton.Visibility = canDelete ? Visibility.Visible : Visibility.Collapsed;

            if (separateLine is bool value)
            {
                SeparateLine.IsChecked = value;
                SeparateLine.Visibility = Visibility.Visible;
            }

            DefaultPreview.SetButton(clientService, null, 0, new ButtonStyleDefault(), null);
            PrimaryPreview.SetButton(clientService, null, 0, new ButtonStylePrimary(), null);
            SuccessPreview.SetButton(clientService, null, 0, new ButtonStyleSuccess(), null);
            DangerPreview.SetButton(clientService, null, 0, new ButtonStyleDanger(), null);

            if (button != null)
            {
                TextField.Text = RichDocumentConverter.GetPlainText(button.Text);

                switch (button.Type)
                {
                    case InlineKeyboardButtonTypeCopyText copy:
                        CopyType.IsChecked = true;
                        ValueField.Text = copy.Text;
                        break;
                    case InlineKeyboardButtonTypeUser user:
                        SetUser(user.UserId);
                        ProfileType.IsChecked = true;
                        break;
                    case InlineKeyboardButtonTypeUrl url:
                        LinkType.IsChecked = true;
                        ValueField.Text = url.Url;
                        break;
                }

                var color = button.Style switch
                {
                    ButtonStylePrimary => PrimaryColor,
                    ButtonStyleSuccess => SuccessColor,
                    ButtonStyleDanger => DangerColor,
                    _ => DefaultColor
                };

                color.IsChecked = true;
            }

            _previousType = ProfileType.IsChecked == true ? LinkType : (CopyType.IsChecked == true ? CopyType : LinkType);
            UpdateType();
        }

        /// <summary>What the popup was closed with; <see cref="Button"/> is set for <see cref="RichButtonPopupResult.Save"/>.</summary>
        public InlineButton Button { get; private set; }

        public bool IsSeparateLine => SeparateLine.IsChecked == true;

        private async void OnTypeChecked(object sender, RoutedEventArgs e)
        {
            UpdateType();

            if (_reverting || TextField == null)
            {
                return;
            }

            if (sender != ProfileType)
            {
                _previousType = sender as RadioButton;
            }
            else if (_userId == 0)
            {
                // A mention needs someone to mention: asked for straight away, and the choice
                // goes back to what it was if nobody is picked, as TDesktop does.
                if (!await ChooseUserAsync())
                {
                    _reverting = true;
                    _previousType.IsChecked = true;
                    _reverting = false;
                }
            }
        }

        private void UpdateType()
        {
            if (TextField == null)
            {
                return;
            }

            var profile = ProfileType.IsChecked == true;
            var copy = CopyType.IsChecked == true;

            ValueField.Visibility = profile ? Visibility.Collapsed : Visibility.Visible;
            UserButton.Visibility = profile && _userId != 0 ? Visibility.Visible : Visibility.Collapsed;

            var placeholder = copy ? Strings.RichEditorButtonCopyText : Strings.RichEditorButtonURL;
            ValueField.PlaceholderText = placeholder;
            ValueField.InputScope = copy ? null : new InputScope { Names = { new InputScopeName(InputScopeNameValue.Url) } };
            AutomationProperties.SetName(ValueField, placeholder);
        }

        private void SetUser(long userId)
        {
            _userId = userId;

            if (_clientService.TryGetUser(userId, out User user))
            {
                UserName.Text = user.FullName();
                UserPhoto.Source = ProfilePictureSource.User(_clientService, user);
            }
            else
            {
                UserName.Text = string.Empty;
                UserPhoto.Source = null;
            }

            AutomationProperties.SetName(UserButton, UserName.Text);
            UpdateType();
        }

        private async Task<bool> ChooseUserAsync()
        {
            var user = await ChooseChatsPopup.PickUserAsync(_clientService, _navigationService, Strings.RichEditorChooseUser, false);
            if (user == null)
            {
                return false;
            }

            SetUser(user.Id);
            return true;
        }

        private async void UserButton_Click(object sender, RoutedEventArgs e)
        {
            await ChooseUserAsync();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            _result = RichButtonPopupResult.Delete;
            Hide(ContentDialogResult.Secondary);
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (!Validate())
            {
                args.Cancel = true;
            }
        }

        private bool Validate()
        {
            var text = TextField.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                VisualUtilities.ShakeView(TextField);
                return false;
            }

            InlineKeyboardButtonType type;

            if (ProfileType.IsChecked == true)
            {
                if (_userId == 0)
                {
                    VisualUtilities.ShakeView(ProfileType);
                    return false;
                }

                type = new InlineKeyboardButtonTypeUser(_userId);
            }
            else if (CopyType.IsChecked == true)
            {
                var copy = ValueField.Text?.Trim();
                if (string.IsNullOrEmpty(copy))
                {
                    VisualUtilities.ShakeView(ValueField);
                    return false;
                }

                type = new InlineKeyboardButtonTypeCopyText(copy);
            }
            else
            {
                if (!ValueField.Text.IsValidUrl())
                {
                    VisualUtilities.ShakeView(ValueField);
                    return false;
                }

                type = new InlineKeyboardButtonTypeUrl(ValueField.Text);
            }

            ButtonStyle style = PrimaryColor.IsChecked == true
                ? new ButtonStylePrimary()
                : SuccessColor.IsChecked == true
                ? new ButtonStyleSuccess()
                : DangerColor.IsChecked == true
                ? new ButtonStyleDanger()
                : new ButtonStyleDefault();

            Button = new InlineButton(new RichTextPlain(text), style, type);
            _result = RichButtonPopupResult.Save;
            return true;
        }

        private void TextField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (ValueField.Visibility == Visibility.Visible)
                {
                    ValueField.Focus(FocusState.Keyboard);
                }
                else if (Validate())
                {
                    Hide(ContentDialogResult.Primary);
                }

                e.Handled = true;
            }
        }

        private void ValueField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (Validate())
                {
                    Hide(ContentDialogResult.Primary);
                }

                e.Handled = true;
            }
        }

        private void TextField_Loaded(object sender, RoutedEventArgs e)
        {
            TextField.Focus(FocusState.Keyboard);
        }

        public async Task<RichButtonPopupResult> ShowQueuedAsync(XamlRoot xamlRoot)
        {
            _result = RichButtonPopupResult.Cancel;

            await ShowAsync(xamlRoot);
            return _result;
        }
    }
}
