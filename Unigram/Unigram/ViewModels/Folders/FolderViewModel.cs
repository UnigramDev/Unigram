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
            Include = new MvxObservableCollection<ChatListFilterElement>();
            Exclude = new MvxObservableCollection<ChatListFilterElement>();

            RemoveIncludeCommand = new RelayCommand<ChatListFilterElement>(RemoveIncludeExecute);
            RemoveExcludeCommand = new RelayCommand<ChatListFilterElement>(RemoveExcludeExecute);

            AddIncludeCommand = new RelayCommand(AddIncludeExecute);
            AddExcludeCommand = new RelayCommand(AddExcludeExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ChatListFilter filter = null;

            if (parameter is int id)
            {
                var response = await ProtoService.SendAsync(new GetChatListFilters());
                if (response is ChatListFilters filters)
                {
                    filter = filters.Filters.FirstOrDefault(x => x.Id == id);
                }
            }
            else
            {
                filter = new ChatListFilter();
            }

            if (filter == null)
            {
                return;
            }

            Id = filter.Id;
            Title = filter.Title;

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

            foreach (var chatId in filter.IncludeChats)
            {
                var chat = CacheService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                Include.Add(new FilterChat { Chat = chat });
            }

            foreach (var chatId in filter.ExcludeChats)
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
        public string Title { get; set; }

        public MvxObservableCollection<ChatListFilterElement> Include { get; private set; }
        public MvxObservableCollection<ChatListFilterElement> Exclude { get; private set; }



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
                    Text = "[Chat types]",
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
                    Text = "[Chats]",
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
                    Text = "[Chat types]",
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
                    Text = "[Chats]",
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
            title.Text = Enum.GetName(typeof(ChatListFilterFlags), filter.Flag);

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetGlyph(MainPage.GetFilterIcon(filter.Flag), (int)filter.Flag, 36);
        }

        public RelayCommand<ChatListFilterElement> RemoveIncludeCommand { get; }
        private void RemoveIncludeExecute(ChatListFilterElement chat)
        {
            Include.Remove(chat);
        }

        public RelayCommand<ChatListFilterElement> RemoveExcludeCommand { get; }
        private void RemoveExcludeExecute(ChatListFilterElement chat)
        {
            Exclude.Remove(chat);
        }
    }

    public class ChatListFilterElement
    {
    }

    public class FilterFlag : ChatListFilterElement
    {
        public ChatListFilterFlags Flag { get; set; }
    }

    public class FilterChat : ChatListFilterElement
    {
        public Chat Chat { get; set; }
    }
}
