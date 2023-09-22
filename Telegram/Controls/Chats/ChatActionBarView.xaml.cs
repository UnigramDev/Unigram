//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatActionBarView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private UIElement _parent;

        public ChatActionBarView()
        {
            InitializeComponent();
        }

        public void InitializeParent(UIElement parent)
        {
            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
        }

        public void UpdateChatActionBar(Chat chat)
        {
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
                    CreateButton(Strings.ReportSpamUser, ViewModel.ReportSpam, new ReportReasonSpam(), column: 1, danger: true);
                }
                else
                {
                    CreateButton(Strings.ReportSpamUser, ViewModel.ReportSpam, new ReportReasonSpam(), danger: true);
                    CreateButton(Strings.AddContactChat, ViewModel.AddToContacts, column: 1);
                }
            }
            else if (chat.ActionBar is ChatActionBarReportSpam reportSpam)
            {
                var user = ViewModel.ClientService.GetUser(chat);
                if (user != null)
                {
                    CreateButton(Strings.ReportSpamUser, ViewModel.ReportSpam, new ReportReasonSpam(), danger: true);
                }
                else
                {
                    CreateButton(Strings.ReportSpamAndLeave, ViewModel.ReportSpam, new ReportReasonSpam(), danger: true);
                }
            }
            else if (chat.ActionBar is ChatActionBarReportUnrelatedLocation)
            {
                CreateButton(Strings.ReportSpamLocation, ViewModel.ReportSpam, new ReportReasonUnrelatedLocation(), danger: true);
            }
            else if (chat.ActionBar is ChatActionBarSharePhoneNumber)
            {
                CreateButton(Strings.ShareMyPhone, ViewModel.ShareMyContact);
            }

            ShowHide(chat.ActionBar != null);
        }

        private Button CreateButton<T>(string text, Action<T> command, T commandParameter = null, int column = 0, bool danger = false) where T : class
        {
            var button = CreateButton(text, column, danger);

            void handler(object sender, RoutedEventArgs e)
            {
                button.Click -= handler;
                command(commandParameter);
            };

            button.Click += handler;
            return button;
        }

        private Button CreateButton(string text, Action command, int column = 0, bool danger = false)
        {
            var button = CreateButton(text, column, danger);

            void handler(object sender, RoutedEventArgs e)
            {
                //button.Click -= handler;
                command();
            };

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


        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            var parent = ElementCompositionPreview.GetElementVisual(_parent);
            var visual = ElementCompositionPreview.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (show)
                {
                    _collapsed = false;
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 32);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -32, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
