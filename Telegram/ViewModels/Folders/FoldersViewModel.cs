//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Telegram.Views.Folders;
using Telegram.Views.Folders.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Folders
{
    public enum FoldersPlacement
    {
        Top,
        Left
    }

    public class FoldersViewModel : ViewModelBase, IHandle
    {
        public FoldersViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _placement = Settings.IsLeftTabsEnabled
                ? FoldersPlacement.Left
                : FoldersPlacement.Top;

            Items = new MvxObservableCollection<ChatFolderInfo>();
            Recommended = new MvxObservableCollection<RecommendedChatFolder>();
        }

        public MvxObservableCollection<ChatFolderInfo> Items { get; private set; }
        public MvxObservableCollection<RecommendedChatFolder> Recommended { get; private set; }

        private bool _canCreateNew;
        public bool CanCreateNew
        {
            get => _canCreateNew;
            set => Set(ref _canCreateNew, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            Items.ReplaceWith(ClientService.ChatFolders);

            if (ClientService.IsPremiumAvailable)
            {
                var limit = await ClientService.SendAsync(new GetPremiumLimit(new PremiumLimitTypeChatFolderCount())) as PremiumLimit;
                CanCreateNew = Items.Count < limit.PremiumValue;
            }
            else
            {
                CanCreateNew = Items.Count < ClientService.Options.ChatFolderCountMax;
            }

            if (ClientService.Options.ChatFolderCountMax > Items.Count)
            {
                var response = await ClientService.SendAsync(new GetRecommendedChatFolders());
                if (response is RecommendedChatFolders folders)
                {
                    Recommended.ReplaceWith(folders.ChatFolders);
                }
            }
            else
            {
                Recommended.Clear();
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateChatFolders>(this, Handle);
        }

        private FoldersPlacement _placement;
        public FoldersPlacement Placement
        {
            get => _placement;
            set => SetPlacement(value);
        }

        private void SetPlacement(FoldersPlacement value, bool update = true)
        {
            if (Set(ref _placement, value, nameof(Placement)))
            {
                RaisePropertyChanged(nameof(UseTopLayout));
                RaisePropertyChanged(nameof(UseLeftLayout));

                if (update)
                {
                    Settings.IsLeftTabsEnabled = value == FoldersPlacement.Left;
                    Aggregator.Publish(new UpdateChatFoldersLayout());
                }
            }
        }

        public bool UseTopLayout
        {
            get => _placement == FoldersPlacement.Top;
            set
            {
                if (value)
                {
                    SetPlacement(FoldersPlacement.Top);
                }
            }
        }

        public bool UseLeftLayout
        {
            get => _placement == FoldersPlacement.Left;
            set
            {
                if (value)
                {
                    SetPlacement(FoldersPlacement.Left);
                }
            }
        }

        public void Handle(UpdateChatFolders update)
        {
            BeginOnUIThread(async () =>
            {
                Items.ReplaceWith(update.ChatFolders);

                if (Items.Count < 10)
                {
                    var response = await ClientService.SendAsync(new GetRecommendedChatFolders());
                    if (response is RecommendedChatFolders recommended)
                    {
                        Recommended.ReplaceWith(recommended.ChatFolders);
                    }
                }
                else
                {
                    Recommended.Clear();
                }
            });
        }

        public void AddRecommended(RecommendedChatFolder folder)
        {
            Recommended.Remove(folder);
            ClientService.Send(new CreateChatFolder(folder.Folder));
        }

        public void Edit(ChatFolderInfo folder)
        {
            var index = Items.IndexOf(folder);
            if (index < ClientService.Options.ChatFolderCountMax)
            {
                NavigationService.Navigate(typeof(FolderPage), folder.Id);
            }
            else
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypeChatFolderCount());
            }
        }

        public void Delete(ChatFolderInfo info)
        {
            Delete(ClientService, NavigationService, info);
        }

        public static async void Delete(IClientService clientService, INavigationService navigationService, ChatFolderInfo info)
        {
            var response = await clientService.SendAsync(new GetChatFolderChatsToLeave(info.Id));
            if (response is Td.Api.Chats leave && leave.TotalCount > 0)
            {
                var responsee = await clientService.SendAsync(new GetChatFolder(info.Id));
                if (responsee is ChatFolder folder)
                {
                    var tsc = new TaskCompletionSource<object>();

                    var confirm = await navigationService.ShowPopupAsync(typeof(RemoveFolderPopup), Tuple.Create(folder, leave), tsc);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        var result = await tsc.Task;
                        if (result is IList<long> chats)
                        {
                            clientService.Send(new DeleteChatFolder(info.Id, chats));
                        }
                    }
                }
            }
            else
            {
                var confirm = await MessagePopup.ShowAsync(info.HasMyInviteLinks ? Strings.FilterDeleteAlertLinks : Strings.FilterDeleteAlert, Strings.FilterDelete, Strings.Delete, Strings.Cancel, destructive: true);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                clientService.Send(new DeleteChatFolder(info.Id, Array.Empty<long>()));
            }
        }

        public void Create()
        {
            if (Items.Count < ClientService.Options.ChatFolderCountMax)
            {
                NavigationService.Navigate(typeof(FolderPage));
            }
            else
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypeChatFolderCount());
            }
        }
    }
}
