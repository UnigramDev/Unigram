using System;
using System.Numerics;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Chats
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
                var user = ViewModel.CacheService.GetUser(chat);
                if (user != null)
                {
                    CreateButton(string.Format(Strings.Resources.AddContactFullChat, user.FirstName.ToUpper()), ViewModel.AddContactCommand);
                }
                else
                {
                    CreateButton(Strings.Resources.AddContactChat, ViewModel.AddContactCommand);
                }
            }
            else if (chat.ActionBar is ChatActionBarInviteMembers)
            {
                CreateButton(Strings.Resources.GroupAddMembers.ToUpper(), ViewModel.InviteCommand);
            }
            else if (chat.ActionBar is ChatActionBarJoinRequest joinRequest)
            {

            }
            else if (chat.ActionBar is ChatActionBarReportAddBlock reportAddBlock)
            {
                if (reportAddBlock.CanUnarchive)
                {
                    CreateButton(Strings.Resources.Unarchive.ToUpper(), ViewModel.UnarchiveCommand);
                    CreateButton(Strings.Resources.ReportSpamUser, ViewModel.ReportSpamCommand, column: 1, danger: true);
                }
                else
                {
                    CreateButton(Strings.Resources.ReportSpamUser, ViewModel.ReportSpamCommand, danger: true);
                    CreateButton(Strings.Resources.AddContactChat, ViewModel.AddContactCommand, column: 1);
                }
            }
            else if (chat.ActionBar is ChatActionBarReportSpam reportSpam)
            {
                var user = ViewModel.CacheService.GetUser(chat);
                if (user != null)
                {
                    CreateButton(Strings.Resources.ReportSpamUser, ViewModel.ReportSpamCommand, danger: true);
                }
                else
                {
                    CreateButton(Strings.Resources.ReportSpamAndLeave, ViewModel.ReportSpamCommand, new ChatReportReasonSpam(), danger: true);
                }
            }
            else if (chat.ActionBar is ChatActionBarReportUnrelatedLocation)
            {
                CreateButton(Strings.Resources.ReportSpamLocation, ViewModel.ReportSpamCommand, new ChatReportReasonUnrelatedLocation(), danger: true);
            }
            else if (chat.ActionBar is ChatActionBarSharePhoneNumber)
            {
                CreateButton(Strings.Resources.ShareMyPhone, ViewModel.ShareContactCommand);
            }

            ShowHide(chat.ActionBar != null);
        }

        private Button CreateButton(string text, ICommand command, object commandParameter = null, int column = 0, bool danger = false)
        {
            var label = new TextBlock();
            label.Style = BootStrapper.Current.Resources["CaptionTextBlockStyle"] as Style;
            label.Foreground = BootStrapper.Current.Resources[danger ? "DangerButtonBackground" : "SystemControlHighlightAccentBrush"] as Brush;
            label.Text = text;

            var button = new Button();
            button.Style = BootStrapper.Current.Resources["EmptyButtonStyle"] as Style;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Content = label;
            button.Command = command;
            button.CommandParameter = commandParameter;

            LayoutRoot.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(button, column);

            LayoutRoot.Children.Add(button);
            return button;
        }


        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if ((show && Visibility == Visibility.Visible) || (!show && (Visibility == Visibility.Collapsed || _collapsed)))
            {
                return;
            }

            if (show)
            {
                _collapsed = false;
            }
            else
            {
                _collapsed = true;
            }

            Visibility = Visibility.Visible;

            var visual = ElementCompositionPreview.GetElementVisual(_parent);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                //visual.Offset = new Vector3();
                visual.Properties.InsertVector3("Translation", Vector3.Zero);

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
            clip.InsertKeyFrame(show ? 0 : 1, 40);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = TimeSpan.FromMilliseconds(150);

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = TimeSpan.FromMilliseconds(150);

            visual.Clip.StartAnimation("TopInset", clip);
            visual.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
