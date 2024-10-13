using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Popups
{
    public record ReportChatSelection(ReportOption Option, IList<long> MessageIds, string Text, object Result);

    public sealed partial class ReportChatPopup : ContentPopup
    {
        private readonly TaskCompletionSource<ReportChatSelection> _task = new();
        private bool _collapsed = true;

        private readonly IClientService _clientService;
        private readonly long _chatId;

        private readonly Stack<ReportChatSelection> _history = new();
        private ReportChatSelection _selection;

        public ReportChatPopup(IClientService clientService, INavigationService navigationService, long chatId, ReportOption option, IList<long> messageIds, string text)
        {
            InitializeComponent();
            XamlRoot = navigationService.XamlRoot;

            _clientService = clientService;
            _chatId = chatId;

            option ??= new ReportOption(Array.Empty<byte>(), Strings.Report2);

            Title.Text = option.Text;

            Continue(option, messageIds, text);
        }

        public Task<ReportChatSelection> ReportAsync()
        {
            return _task.Task;
        }

        private async void Continue(ReportOption option, IList<long> messageIds, string text)
        {
            IsEnabled = false;

            if (_selection != null)
            {
                _history.Push(_selection);
            }

            var response = await _clientService.SendAsync(new ReportChat(_chatId, option.Id, messageIds, text));
            if (response is ReportChatResultTextRequired textRequired)
            {
                option = new ReportOption(textRequired.OptionId, option.Text);
            }

            UpdateSelection(new ReportChatSelection(option, messageIds, text, response));

            IsEnabled = true;
        }

        private void UpdateSelection(ReportChatSelection selection)
        {
            _selection = selection;
            Title.Text = selection.Option.Text;

            if (_collapsed && selection.Result is not ReportChatResultMessagesRequired and not ReportChatResultOk)
            {
                _collapsed = false;
                _ = this.ShowQueuedAsync(XamlRoot);
            }

            if (selection.Result is ReportChatResultOptionRequired optionRequired)
            {
                OptionRoot.Visibility = Visibility.Visible;
                TextRoot.Visibility = Visibility.Collapsed;

                Subtitle.Text = optionRequired.Title;
                ScrollingHost.ItemsSource = optionRequired.Options;
            }
            else if (selection.Result is ReportChatResultTextRequired textRequired)
            {
                Animated.Play();

                OptionRoot.Visibility = Visibility.Collapsed;
                TextRoot.Visibility = Visibility.Visible;

                Send.IsEnabled = textRequired.IsOptional;

                Text.PlaceholderText = textRequired.IsOptional
                    ? Strings.Report2CommentOptional
                    : Strings.Report2Comment;

                if (_clientService.TryGetChat(_chatId, out Chat chat))
                {
                    if (chat.Type is ChatTypePrivate or ChatTypeSecret)
                    {
                        TextInfo.Text = Strings.Report2CommentInfoUser;
                    }
                    else if (chat.Type is ChatTypeBasicGroup)
                    {
                        TextInfo.Text = Strings.Report2CommentInfoGroup;
                    }
                    else if (chat.Type is ChatTypeSupergroup supergroup)
                    {
                        TextInfo.Text = supergroup.IsChannel
                            ? Strings.Report2CommentInfoChannel
                            : Strings.Report2CommentInfoGroup;
                    }
                    else
                    {
                        TextInfo.Text = Strings.Report2CommentInfo;
                    }
                }
                else
                {
                    TextInfo.Text = Strings.Report2CommentInfo;
                }
            }
            else if (selection.Result is ReportChatResultMessagesRequired or ReportChatResultOk)
            {
                _task.TrySetResult(selection);
                Hide();

                if (selection.Result is ReportChatResultOk)
                {
                    ToastPopup.Show(XamlRoot, Strings.Reported2, ToastPopupIcon.AntiSpam);
                }
            }

            ShowHideBackButton(_history.Count > 0);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ReportOption option && _selection != null)
            {
                Continue(option, _selection.MessageIds, _selection.Text);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is ReportOption option)
            {
                var textBlock = content.Children[0] as TextBlock;
                textBlock.Text = option.Text;
            }

            args.Handled = true;
        }

        private void Text_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selection?.Result is ReportChatResultTextRequired textRequired)
            {
                Send.IsEnabled = textRequired.IsOptional || !string.IsNullOrWhiteSpace(Text.Text);
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (_selection != null)
            {
                Continue(_selection.Option, _selection.MessageIds, Text.Text);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_history.TryPop(out ReportChatSelection selection))
            {
                UpdateSelection(selection);
            }
        }

        private bool _backButtonCollapsed = true;

        private void ShowHideBackButton(bool show)
        {
            if (_backButtonCollapsed != show)
            {
                return;
            }

            _backButtonCollapsed = !show;
            BackButton.Visibility = Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(BackButton);
            var visual2 = ElementComposition.GetElementVisual(Title);

            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            var batch = visual1.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual2.Properties.InsertVector3("Translation", Vector3.Zero);
                BackButton.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            var offset = visual1.Compositor.CreateScalarKeyFrameAnimation();
            offset.InsertKeyFrame(0, show ? -28 : 0);
            offset.InsertKeyFrame(1, show ? 0 : -28);
            offset.Duration = Constants.FastAnimation;

            var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale.Duration = Constants.FastAnimation;

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);

            visual1.CenterPoint = new Vector3(24);

            visual2.StartAnimation("Translation.X", offset);
            visual1.StartAnimation("Scale", scale);
            visual1.StartAnimation("Opacity", opacity);
            batch.End();
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _task.TrySetResult(null);
        }
    }
}
