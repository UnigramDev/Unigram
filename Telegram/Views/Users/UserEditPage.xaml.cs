//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Users;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Users
{
    public sealed partial class UserEditPage : HostedPage, IUserDelegate
    {
        public UserEditViewModel ViewModel => DataContext as UserEditViewModel;

        public UserEditPage()
        {
            InitializeComponent();
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            Photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);

            if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
            {
                FindName(nameof(BotPhoto));

                FindName(nameof(About));

                FindName(nameof(UsernamePanel));
                FindName(nameof(BotPanel));

                if (fullInfo?.BotInfo?.VerificationParameters != null)
                {
                    FindName(nameof(BotPanel2));
                }
                else
                {
                    BotPanel2?.Visibility = Visibility.Collapsed;
                }

                LayoutRoot.Footer = string.Empty;

                Username.Content = Strings.BotPublicLink;
                Username.Badge = MeUrlPrefixConverter.Convert(ViewModel.ClientService, user.ActiveUsername(), true);

                FirstName.PlaceholderText = Strings.BotName;
                FirstName.VerticalAlignment = VerticalAlignment.Center;
                FirstName.Margin = new Thickness();

                Grid.SetRowSpan(FirstName, 2);
            }
            else
            {
                FindName(nameof(PhotoPanel));
                FindName(nameof(LastName));

                if (NotePanel == null)
                {
                    FindName(nameof(NotePanel));

                    EmojiPanel.DataContext = EmojiDrawerViewModel.Create(ViewModel.Session);
                    NoteField.AllowedEntities = FormattedTextEntity.Bold | FormattedTextEntity.Italic | FormattedTextEntity.Underline | FormattedTextEntity.Strikethrough | FormattedTextEntity.Spoiler | FormattedTextEntity.CustomEmoji;
                    NoteField.CustomEmoji = CustomEmoji;
                    NoteField.MaxLength = (int)ViewModel.ClientService.Options.UserNoteTextLengthMax;
                }

                SuggestPhoto.Content = string.Format(Strings.SuggestPhotoFor, user.FirstName);
                PersonalPhoto.Content = string.Format(Strings.SetPhotoFor, user.FirstName);
            }

            if (fullInfo == null)
            {
                return;
            }

            NoteField?.SetText(fullInfo.Note);

            if (ResetPhoto != null)
            {
                if (fullInfo.PersonalPhoto != null)
                {
                    ResetPhotoPhoto.Source = ProfilePictureSource.ChatPhoto(ViewModel.ClientService, user, fullInfo.Photo, false);
                    ResetPhotoPhoto.Visibility = Visibility.Visible;
                    ResetPhoto.Visibility = Visibility.Visible;
                }
                else
                {
                    ResetPhotoPhoto.Visibility = Visibility.Collapsed;
                    ResetPhoto.Visibility = Visibility.Collapsed;
                }

                SuggestPhoto.Visibility = fullInfo.OutgoingPaidMessageStarCount > 0
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                SuggestBirthday.Visibility = fullInfo.OutgoingPaidMessageStarCount > 0 || fullInfo.Birthdate != null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (fullInfo.NeedPhoneNumberPrivacyException)
            {
                FindName(nameof(SharePhonePanel));

                SharePhoneCheck.Content = string.Format(Strings.SharePhoneNumberWith, user.FirstName);
            }

            if (fullInfo.BotInfo?.AffiliateProgram != null)
            {
                AffiliateProgram.Badge = fullInfo.BotInfo.AffiliateProgram.Parameters.CommissionPercent();
            }
            else
            {
                AffiliateProgram?.Badge = Strings.AffiliateProgramBotOff;
            }
        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion

        public string ConvertStarCount(StarAmount amount)
        {
            if (amount != null)
            {
                return amount.ToValue();
            }

            return null;
        }

        private void NoteField_TextChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.Note = NoteField.GetFormattedText();
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Transient,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight
            });
        }

        private void Emoji_ItemClick(object sender, Controls.Drawers.EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                NoteField.InsertText(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                NoteField.InsertEmoji(sticker);
            }

            NoteField.Focus(FocusState.Programmatic);
        }
    }
}
