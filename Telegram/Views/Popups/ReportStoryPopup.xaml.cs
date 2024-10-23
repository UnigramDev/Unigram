using System;
using System.Collections.Generic;
using System.Numerics;
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
    public record ReportStorySelection(ReportOption Option, string Text, object Result);

    public sealed partial class ReportStoryPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly long _storySenderChatId;
        private readonly int _storyId;

        private readonly Stack<ReportStorySelection> _history = new();
        private ReportStorySelection _selection;

        public ReportStoryPopup(IClientService clientService, INavigationService navigationService, long storySenderChatId, int storyId, ReportOption option, string text)
        {
            InitializeComponent();
            XamlRoot = navigationService.XamlRoot;

            _clientService = clientService;
            _storySenderChatId = storySenderChatId;
            _storyId = storyId;

            option ??= new ReportOption(Array.Empty<byte>(), Strings.Report2);

            Title.Text = option.Text;

            Continue(option, text);
        }

        private async void Continue(ReportOption option, string text)
        {
            IsEnabled = false;

            if (_selection != null)
            {
                _history.Push(_selection);
            }

            var response = await _clientService.SendAsync(new ReportStory(_storySenderChatId, _storyId, option.Id, text));
            if (response is ReportStoryResultTextRequired textRequired)
            {
                option = new ReportOption(textRequired.OptionId, option.Text);
            }

            UpdateSelection(new ReportStorySelection(option, text, response));

            IsEnabled = true;
        }

        private void UpdateSelection(ReportStorySelection selection)
        {
            _selection = selection;
            Title.Text = selection.Option.Text;

            if (selection.Result is ReportStoryResultOptionRequired optionRequired)
            {
                OptionRoot.Visibility = Visibility.Visible;
                TextRoot.Visibility = Visibility.Collapsed;

                Subtitle.Text = optionRequired.Title;
                ScrollingHost.ItemsSource = optionRequired.Options;
            }
            else if (selection.Result is ReportStoryResultTextRequired textRequired)
            {
                Animated.Play();

                OptionRoot.Visibility = Visibility.Collapsed;
                TextRoot.Visibility = Visibility.Visible;

                Send.IsEnabled = textRequired.IsOptional;

                Text.PlaceholderText = textRequired.IsOptional
                    ? Strings.Report2CommentOptional
                    : Strings.Report2Comment;

                TextInfo.Text = Strings.Report2CommentInfoUser;
            }
            else if (selection.Result is ReportStoryResultOk)
            {
                Hide();
                ToastPopup.Show(XamlRoot, Strings.Reported2, ToastPopupIcon.AntiSpam);
            }

            ShowHideBackButton(_history.Count > 0);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ReportOption option && _selection != null)
            {
                Continue(option, _selection.Text);
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
            if (_selection?.Result is ReportStoryResultTextRequired textRequired)
            {
                Send.IsEnabled = textRequired.IsOptional || !string.IsNullOrWhiteSpace(Text.Text);
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (_selection != null)
            {
                Continue(_selection.Option, Text.Text);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_history.TryPop(out ReportStorySelection selection))
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
    }
}
