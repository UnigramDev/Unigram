using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Folders
{
    public class FolderViewModel : TLViewModelBase
    {
        public FolderViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Include = new MvxObservableCollection<ChatFilterElement>();
            Exclude = new MvxObservableCollection<ChatFilterElement>();

            Include.CollectionChanged += OnCollectionChanged;
            Exclude.CollectionChanged += OnCollectionChanged;

            RemoveIncludeCommand = new RelayCommand<ChatFilterElement>(RemoveIncludeExecute);
            RemoveExcludeCommand = new RelayCommand<ChatFilterElement>(RemoveExcludeExecute);

            AddIncludeCommand = new RelayCommand(AddIncludeExecute);
            AddExcludeCommand = new RelayCommand(AddExcludeExecute);

            SendCommand = new RelayCommand(SendExecute, SendCanExecute);
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SendCommand.RaiseCanExecuteChanged();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ChatFilter filter = null;

            if (parameter is int id)
            {
                var response = await ProtoService.SendAsync(new GetChatFilter(id));
                if (response is ChatFilter result)
                {
                    Id = id;
                    filter = result;
                }
                else
                {
                    // TODO
                }
            }
            else
            {
                filter = new ChatFilter();
            }

            if (filter == null)
            {
                return;
            }

            Title = filter.Title;
            Emoji = filter.Emoji ?? ChatFilterIcon.Default;

            Include.Clear();
            Exclude.Clear();

            if (filter.IncludeContacts)    Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeContacts });
            if (filter.IncludeNonContacts) Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeNonContacts });
            if (filter.IncludeGroups)      Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeGroups });
            if (filter.IncludeChannels)    Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeChannels });
            if (filter.IncludeBots)        Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeBots });
            if (filter.ExcludeMuted)       Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeMuted});
            if (filter.ExcludeRead)        Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeRead });
            if (filter.ExcludeArchived)    Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeArchived });

            foreach (var chatId in filter.IncludeChatIds)
            {
                var chat = CacheService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                Include.Add(new FilterChat { Chat = chat });
            }

            foreach (var chatId in filter.ExcludeChatIds)
            {
                var chat = CacheService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                Exclude.Add(new FilterChat { Chat = chat });
            }
        }

        public int? Id { get; set; }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                Set(ref _title, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private string _emoji;
        public string Emoji
        {
            get => _emoji;
            set => Set(ref _emoji, value);
        }

        public MvxObservableCollection<ChatFilterElement> Include { get; private set; }
        public MvxObservableCollection<ChatFilterElement> Exclude { get; private set; }



        public RelayCommand AddIncludeCommand { get; }
        private async void AddIncludeExecute()
        {
            var flags = new List<FilterFlag>();
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeContacts });
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeNonContacts });
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeGroups });
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeChannels });
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeBots });

            var header = new ListView();
            header.SelectionMode = ListViewSelectionMode.Multiple;
            header.ItemsSource = flags;
            header.ItemTemplate = App.Current.Resources["FolderPickerTemplate"] as DataTemplate;
            header.ItemContainerStyle = App.Current.Resources["DefaultListViewItemStyle"] as Style;
            header.ContainerContentChanging += Header_ContainerContentChanging;

            foreach (var filter in Include.OfType<FilterFlag>())
            {
                var already = flags.FirstOrDefault(x => x.Flag == filter.Flag);
                if (already != null)
                {
                    header.SelectedItems.Add(already);
                }
            }

            var panel = new StackPanel();
            panel.Children.Add(new Border
            {
                Background = App.Current.Resources["PageBackgroundDarkBrush"] as Brush,
                Child = new TextBlock
                {
                    Text = Strings.Resources.FilterChatTypes,
                    Padding = new Thickness(12, 0, 0, 0),
                    Style = App.Current.Resources["SettingsGroupTextBlockStyle"] as Style
                }
            });
            panel.Children.Add(header);
            panel.Children.Add(new Border
            {
                Background = App.Current.Resources["PageBackgroundDarkBrush"] as Brush,
                Child = new TextBlock
                {
                    Text = Strings.Resources.FilterChats,
                    Padding = new Thickness(12, 0, 0, 0),
                    Style = App.Current.Resources["SettingsGroupTextBlockStyle"] as Style
                }
            });

            var dialog = ShareView.GetForCurrentView();
            dialog.ViewModel.Title = Strings.Resources.FilterAlwaysShow;
            dialog.Header = panel;

            var confirm = await dialog.PickAsync(Include.OfType<FilterChat>().Select(x => x.Chat.Id).ToArray(), SearchChatsType.All);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Include.Clear();

            foreach (var filter in header.SelectedItems.OfType<FilterFlag>())
            {
                Include.Add(filter);
            }

            foreach (var chat in dialog.ViewModel.SelectedItems)
            {
                Include.Add(new FilterChat { Chat = chat });
            }
        }

        public RelayCommand AddExcludeCommand { get; }
        private async void AddExcludeExecute()
        {
            var flags = new List<FilterFlag>();
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeMuted });
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeRead });
            flags.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeArchived });

            var header = new ListView();
            header.SelectionMode = ListViewSelectionMode.Multiple;
            header.ItemsSource = flags;
            header.ItemTemplate = App.Current.Resources["FolderPickerTemplate"] as DataTemplate;
            header.ItemContainerStyle = App.Current.Resources["DefaultListViewItemStyle"] as Style;
            header.ContainerContentChanging += Header_ContainerContentChanging;

            foreach (var filter in Exclude.OfType<FilterFlag>())
            {
                var already = flags.FirstOrDefault(x => x.Flag == filter.Flag);
                if (already != null)
                {
                    header.SelectedItems.Add(already);
                }
            }

            var panel = new StackPanel();
            panel.Children.Add(new Border
            {
                Background = App.Current.Resources["PageBackgroundDarkBrush"] as Brush,
                Child = new TextBlock
                {
                    Text = Strings.Resources.FilterChatTypes,
                    Padding = new Thickness(12, 0, 0, 0),
                    Style = App.Current.Resources["SettingsGroupTextBlockStyle"] as Style
                }
            });
            panel.Children.Add(header);
            panel.Children.Add(new Border
            {
                Background = App.Current.Resources["PageBackgroundDarkBrush"] as Brush,
                Child = new TextBlock
                {
                    Text = Strings.Resources.FilterChats,
                    Padding = new Thickness(12, 0, 0, 0),
                    Style = App.Current.Resources["SettingsGroupTextBlockStyle"] as Style
                }
            });

            var dialog = ShareView.GetForCurrentView();
            dialog.ViewModel.Title = Strings.Resources.FilterNeverShow;
            dialog.Header = panel;

            var confirm = await dialog.PickAsync(Exclude.OfType<FilterChat>().Select(x => x.Chat.Id).ToArray(), SearchChatsType.All);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Exclude.Clear();

            foreach (var filter in header.SelectedItems.OfType<FilterFlag>())
            {
                Exclude.Add(filter);
            }

            foreach (var chat in dialog.ViewModel.SelectedItems)
            {
                Exclude.Add(new FilterChat { Chat = chat });
            }
        }

        private void Header_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var filter = args.Item as FilterFlag;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var title = content.Children[1] as TextBlock;
            //title.Text = Enum.GetName(typeof(ChatListFilterFlags), filter.Flag);

            switch (filter.Flag)
            {
                case ChatListFilterFlags.IncludeContacts:
                    title.Text = Strings.Resources.FilterContacts;
                    break;
                case ChatListFilterFlags.IncludeNonContacts:
                    title.Text = Strings.Resources.FilterNonContacts;
                    break;
                case ChatListFilterFlags.IncludeGroups:
                    title.Text = Strings.Resources.FilterGroups;
                    break;
                case ChatListFilterFlags.IncludeChannels:
                    title.Text = Strings.Resources.FilterChannels;
                    break;
                case ChatListFilterFlags.IncludeBots:
                    title.Text = Strings.Resources.FilterBots;
                    break;

                case ChatListFilterFlags.ExcludeMuted:
                    title.Text = Strings.Resources.FilterMuted;
                    break;
                case ChatListFilterFlags.ExcludeRead:
                    title.Text = Strings.Resources.FilterRead;
                    break;
                case ChatListFilterFlags.ExcludeArchived:
                    title.Text = Strings.Resources.FilterArchived;
                    break;
            }

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetGlyph(MainPage.GetFilterIcon(filter.Flag), (int)filter.Flag, 36);
        }

        public RelayCommand<ChatFilterElement> RemoveIncludeCommand { get; }
        private void RemoveIncludeExecute(ChatFilterElement chat)
        {
            Include.Remove(chat);
        }

        public RelayCommand<ChatFilterElement> RemoveExcludeCommand { get; }
        private void RemoveExcludeExecute(ChatFilterElement chat)
        {
            Exclude.Remove(chat);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var include = new List<long>();
            var exclude = new List<long>();

            var filter = new ChatFilter();
            filter.Title = Title;
            filter.Emoji = Emoji;

            foreach (var item in Include)
            {
                if (item is FilterFlag flag)
                {
                    switch (flag.Flag)
                    {
                        case ChatListFilterFlags.IncludeContacts:
                            filter.IncludeContacts = true;
                            break;
                        case ChatListFilterFlags.IncludeNonContacts:
                            filter.IncludeNonContacts = true;
                            break;
                        case ChatListFilterFlags.IncludeGroups:
                            filter.IncludeGroups = true;
                            break;
                        case ChatListFilterFlags.IncludeChannels:
                            filter.IncludeChannels = true;
                            break;
                        case ChatListFilterFlags.IncludeBots:
                            filter.IncludeBots = true;
                            break;
                    }
                }
                else if (item is FilterChat chat)
                {
                    include.Add(chat.Chat.Id);
                }
            }

            foreach (var item in Include)
            {
                if (item is FilterFlag flag)
                {
                    switch (flag.Flag)
                    {
                        case ChatListFilterFlags.ExcludeMuted:
                            filter.ExcludeMuted = true;
                            break;
                        case ChatListFilterFlags.ExcludeRead:
                            filter.ExcludeRead = true;
                            break;
                        case ChatListFilterFlags.ExcludeArchived:
                            filter.ExcludeArchived = true;
                            break;
                    }
                }
                else if (item is FilterChat chat)
                {
                    exclude.Add(chat.Chat.Id);
                }
            }

            filter.IncludeChatIds = include;
            filter.ExcludeChatIds = exclude;

            Function function;
            if (Id is int id)
            {
                function = new EditChatFilter(id, filter);
            }
            else
            {
                function = new CreateChatFilter(filter);
            }

            var response = await ProtoService.SendAsync(function);
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else
            {

            }
        }

        private bool SendCanExecute()
        {
            return !string.IsNullOrEmpty(Title) &&
                Include.Count > 0 ||
                Exclude.Count > 0;
        }
    }

    public class ChatFilterElement
    {
    }

    public class FilterFlag : ChatFilterElement
    {
        public ChatListFilterFlags Flag { get; set; }
    }

    public class FilterChat : ChatFilterElement
    {
        public Chat Chat { get; set; }
    }
}
