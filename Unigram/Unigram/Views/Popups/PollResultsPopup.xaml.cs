//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class PollResultsPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly IMessageDelegate _delegate;

        public PollResultsPopup(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IMessageDelegate delegato, long chatId, long messageId, Poll poll)
        {
            InitializeComponent();

            _clientService = clientService;
            _delegate = delegato;

            Title = Strings.Resources.PollResults;
            Subtitle.Text = Locale.Declension(poll.Type is PollTypeQuiz ? "Answer" : "Vote", poll.TotalVoterCount);

            PrimaryButtonText = Strings.Resources.OK;

            var options = new List<PollResultViewModel>();
            foreach (var option in poll.Options)
            {
                options.Add(new PollResultViewModel(chatId, messageId, poll, option, clientService, settingsService, aggregator));
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
                var content = button.Content as UserCell;

                content.UpdateUser(_clientService, user, 36);
                button.Click += User_Click;

                AutomationProperties.SetName(button, user.FullName());
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

        public PollResultViewModel(long chatId, long messageId, Poll poll, PollOption option, IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
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

            var response = await ClientService.SendAsync(new GetPollVoters(_chatId, _messageId, _poll.Options.IndexOf(_option), _offset, limit));
            if (response is Telegram.Td.Api.Users users)
            {
                foreach (var id in users.UserIds)
                {
                    var user = ClientService.GetUser(id);
                    if (user == null)
                    {
                        continue;
                    }

                    Items.Add(user);
                }

                _offset += users.UserIds.Count;
                _remaining = users.TotalCount - _offset;

                RaisePropertyChanged(nameof(LoadMoreLabel));
                RaisePropertyChanged(nameof(LoadMoreVisibility));
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
