using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class PostSharesPopup : ContentPopup
    {
        private readonly IProtoService _protoService;
        private readonly IMessageDelegate _delegate;

        public PostSharesPopup(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IMessageDelegate delegato, long chatId, long messageId, Message message)
        {
            InitializeComponent();

            _protoService = protoService;
            _delegate = delegato;

            Title.Text = Strings.Resources.Shares;
            //Subtitle.Text = Locale.Declension(poll.Type is PollTypeQuiz ? "Answer" : "Vote", poll.TotalVoterCount);

            PrimaryButtonText = Strings.Resources.OK;

            //var options = new List<PollResultViewModel>();
            //foreach (var option in poll.Options)
            //{
            //    options.Add(new PollResultViewModel(chatId, messageId, poll, option, protoService, cacheService, settingsService, aggregator));
            //}

            //Repeater.ItemsSource = options;
            Initialize(message);
        }

        private async void Initialize(Message message)
        {
            var response = await _protoService.SendAsync(new GetMessagePublicForwards(message.ChatId, message.Id, string.Empty, 100));
            if (response is FoundMessages messages)
            {

            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var item = sender.ItemsSourceView.GetAt(args.Index);
            if (item is User user)
            {
                var button = args.Element as Button;
                var content = button.Content as Grid;

                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();

                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(_protoService, user, 36);

                button.Click += User_Click;
            }
            else if (item is PollResultViewModel option)
            {
                var headered = args.Element as HeaderedControl;
                headered.Header = $"{option.Text} — {option.VotePercentage}%";
                headered.Footer = Locale.Declension(option.Type is PollTypeQuiz ? "Answer" : "Vote", option.VoterCount);
                headered.Visibility = option.VoterCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void User_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is User user)
            {
                _delegate.OpenUser(user.Id);
                Hide();
            }
        }
    }
}
