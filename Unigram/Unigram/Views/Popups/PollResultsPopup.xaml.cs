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
    public sealed partial class PollResultsPopup : ContentPopup
    {
        private readonly IProtoService _protoService;
        private readonly IMessageDelegate _delegate;

        public PollResultsPopup(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IMessageDelegate delegato, long chatId, long messageId, Poll poll)
        {
            InitializeComponent();

            _protoService = protoService;
            _delegate = delegato;

            Title.Text = Strings.Resources.PollResults;
            Subtitle.Text = Locale.Declension(poll.Type is PollTypeQuiz ? "Answer" : "Vote", poll.TotalVoterCount);

            PrimaryButtonText = Strings.Resources.OK;

            var options = new List<PollResultViewModel>();
            foreach (var option in poll.Options)
            {
                options.Add(new PollResultViewModel(chatId, messageId, poll, option, protoService, cacheService, settingsService, aggregator));
            }

            Repeater.ItemsSource = options;
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

    public class PollResultViewModel : TLViewModelBase
    {
        private readonly long _chatId;
        private readonly long _messageId;
        private readonly Poll _poll;
        private readonly PollOption _option;

        private int _offset;
        private int _remaining;

        public PollResultViewModel(long chatId, long messageId, Poll poll, PollOption option, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _chatId = chatId;
            _messageId = messageId;
            _poll = poll;
            _option = option;

            Items = new MvxObservableCollection<User>();
            LoadMoreCommand = new RelayCommand(LoadMoreExecute);

            LoadMoreExecute();
        }

        public string Text => _option.Text;
        public int VotePercentage => _option.VotePercentage;
        public int VoterCount => _option.VoterCount;

        public PollType Type => _poll.Type;

        public MvxObservableCollection<User> Items { get; private set; }

        public RelayCommand LoadMoreCommand { get; }
        private async void LoadMoreExecute()
        {
            var limit = _option.VoterCount <= 15 ? 15 : 10;
            limit = _offset > 0 ? 50 : limit;

            var response = await ProtoService.SendAsync(new GetPollVoters(_chatId, _messageId, _poll.Options.IndexOf(_option), _offset, limit));
            if (response is Telegram.Td.Api.Users users)
            {
                foreach (var id in users.UserIds)
                {
                    var user = CacheService.GetUser(id);
                    if (user == null)
                    {
                        continue;
                    }

                    Items.Add(user);
                }

                _offset += users.UserIds.Count;
                _remaining = users.TotalCount - _offset;

                RaisePropertyChanged(() => LoadMoreLabel);
                RaisePropertyChanged(() => LoadMoreVisibility);
            }
        }

        public string LoadMoreLabel
        {
            get
            {
                if (_remaining > 0)
                {
                    return Locale.Declension("ShowVotes", _remaining);
                }

                return null;
            }
        }

        public Visibility LoadMoreVisibility
        {
            get
            {
                return _remaining > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
