//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatActionBarView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private ChatView _chatView;

        public ChatActionBarView()
        {
            InitializeComponent();

            _collapsed = new SlidePanel.SlideState(this, false, 32);
        }

        public float AnimatedHeight => _collapsed ? 0 : 32;

        public void InitializeParent(ChatView chatView)
        {
            _chatView = chatView;
        }

        public void UpdateChatActionBar(Chat chat)
        {
            if (chat == null)
            {
                ShowHide(false);
                return;
            }

            if (chat.ActionBar != null)
            {
                LayoutRoot.ColumnDefinitions.Clear();
                LayoutRoot.Children.Clear();
            }

            //ChatActionBarAddContact;
            //ChatActionBarInviteMembers;
            //ChatActionBarJoinRequest;
            //ChatActionBarReportAddBlock;
            //ChatActionBarReportSpam;
            //ChatActionBarReportUnrelatedLocation;
            //ChatActionBarSharePhoneNumber;

            if (chat.ActionBar is ChatActionBarAddContact)
            {
                var user = ViewModel.ClientService.GetUser(chat);
                if (user != null)
                {
                    CreateButton(string.Format(Strings.AddContactFullChat, user.FirstName.ToUpper()), ViewModel.AddToContacts);
                }
                else
                {
                    CreateButton(Strings.AddContactChat, ViewModel.AddToContacts);
                }
            }
            else if (chat.ActionBar is ChatActionBarInviteMembers)
            {
                CreateButton(Strings.GroupAddMembers.ToUpper(), ViewModel.Invite);
            }
            else if (chat.ActionBar is ChatActionBarJoinRequest joinRequest)
            {

            }
            else if (chat.ActionBar is ChatActionBarReportAddBlock reportAddBlock)
            {
                if (reportAddBlock.CanUnarchive)
                {
                    CreateButton(Strings.Unarchive.ToUpper(), ViewModel.Unarchive);
                    CreateButton(Strings.ReportSpamUser, ViewModel.ReportSpam, column: 1, danger: true);
                }
                else
                {
                    CreateButton(Strings.ReportSpamUser, ViewModel.ReportSpam, danger: true);
                    CreateButton(Strings.AddContactChat, ViewModel.AddToContacts, column: 1);
                }
            }
            else if (chat.ActionBar is ChatActionBarReportSpam reportSpam)
            {
                var user = ViewModel.ClientService.GetUser(chat);
                if (user != null)
                {
                    CreateButton(Strings.ReportSpamUser, ViewModel.ReportSpam, danger: true);
                }
                else
                {
                    CreateButton(Strings.ReportSpamAndLeave, ViewModel.ReportSpam, danger: true);
                }
            }
            else if (chat.ActionBar is ChatActionBarSharePhoneNumber)
            {
                CreateButton(Strings.ShareMyPhone, ViewModel.ShareMyContact);
            }

            ShowHide(chat.ActionBar != null);
        }

        private Button CreateButton(string text, Action command, int column = 0, bool danger = false)
        {
            var button = CreateButton(text, column, danger);

            void handler(object sender, RoutedEventArgs e)
            {
                //button.Click -= handler;
                command();
            }

            button.Click += handler;
            return button;
        }

        private Button CreateButton(string text, int column = 0, bool danger = false)
        {
            var label = new TextBlock();
            label.Style = BootStrapper.Current.Resources["CaptionTextBlockStyle"] as Style;
            label.FontWeight = FontWeights.SemiBold;
            label.Text = text;

            var button = new Button();
            button.Style = BootStrapper.Current.Resources[danger ? "DangerTextButtonStyle" : "AccentTextButtonStyle"] as Style;
            button.Background = new SolidColorBrush(Colors.Transparent);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            button.Content = label;

            LayoutRoot.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(button, column);

            LayoutRoot.Children.Add(button);
            return button;
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveActionBar();
        }

        private SlidePanel.SlideState _collapsed;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed.IsVisible = show;
            _chatView.UpdateMessagesHeaderPadding();
        }

        public IEnumerable<UIElement> GetAnimatableVisuals()
        {
            if (_collapsed)
            {
                yield break;
            }

            yield return RemoveButton;
        }
    }
}
