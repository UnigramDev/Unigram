using System.Collections.Generic;
using System.Linq;
using Telegram.Collections;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Telegram.Views.Popups;

namespace Telegram.ViewModels.Business
{
    public enum BusinessRecipientsType
    {
        Exclude,
        Include
    }

    public abstract class BusinessRecipientsViewModelBase : BusinessFeatureViewModelBase
    {
        public BusinessRecipientsViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public bool IsExclude
        {
            get => _recipientsType == BusinessRecipientsType.Exclude;
            set
            {
                if (value)
                {
                    SetRecipientsType(BusinessRecipientsType.Exclude);
                }
            }
        }

        public bool IsInclude
        {
            get => _recipientsType == BusinessRecipientsType.Include;
            set
            {
                if (value)
                {
                    SetRecipientsType(BusinessRecipientsType.Include);
                }
            }
        }

        private BusinessRecipientsType _recipientsType;
        public BusinessRecipientsType RecipientsType
        {
            get => _recipientsType;
            set => SetRecipientsType(value);
        }

        private void SetRecipientsType(BusinessRecipientsType value, bool update = true)
        {
            if (Invalidate(ref _recipientsType, value, nameof(RecipientsType)))
            {
                RaisePropertyChanged(nameof(IsExclude));
                RaisePropertyChanged(nameof(IsInclude));
            }
        }



        public MvxObservableCollection<ChatFolderElement> ExcludedChats { get; } = new();
        public MvxObservableCollection<ChatFolderElement> IncludedChats { get; } = new();

        public async void AddExcluded()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, false, true, true, ExcludedChats.ToList());
            if (result != null)
            {
                ExcludedChats.ReplaceWith(result);
                RaisePropertyChanged(nameof(HasChanged));
            }
        }

        public async void AddIncluded()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, true, true, true, IncludedChats.ToList());
            if (result != null)
            {
                IncludedChats.ReplaceWith(result);
                RaisePropertyChanged(nameof(HasChanged));
            }
        }

        public void RemoveIncluded(ChatFolderElement chat)
        {
            IncludedChats.Remove(chat);
            RaisePropertyChanged(nameof(HasChanged));
        }

        public void RemoveExcluded(ChatFolderElement chat)
        {
            ExcludedChats.Remove(chat);
            RaisePropertyChanged(nameof(HasChanged));
        }

        protected void UpdateRecipients(BusinessRecipients recipients)
        {
            SetRecipientsType(recipients.ExcludeSelected
                ? BusinessRecipientsType.Exclude
                : BusinessRecipientsType.Include);

            IncludedChats.Clear();
            ExcludedChats.Clear();

            var target = recipients.ExcludeSelected
                ? ExcludedChats
                : IncludedChats;

            if (recipients.SelectExistingChats) target.Add(new FolderFlag(ChatListFolderFlags.ExistingChats));
            if (recipients.SelectNewChats) target.Add(new FolderFlag(ChatListFolderFlags.NewChats));
            if (recipients.SelectContacts) target.Add(new FolderFlag(ChatListFolderFlags.IncludeContacts));
            if (recipients.SelectNonContacts) target.Add(new FolderFlag(ChatListFolderFlags.IncludeNonContacts));

            foreach (var chatId in recipients.ChatIds)
            {
                target.Add(new FolderChat(chatId));
            }

            RaisePropertyChanged(nameof(HasChanged));
        }

        protected BusinessRecipients GetRecipients()
        {
            var recipients = new BusinessRecipients
            {
                ExcludeSelected = RecipientsType == BusinessRecipientsType.Exclude,
                ChatIds = new List<long>(),
                ExcludedChatIds = new List<long>()
            };

            var target = recipients.ExcludeSelected
                ? ExcludedChats
                : IncludedChats;

            foreach (var item in target)
            {
                if (item is FolderFlag flag)
                {
                    if (flag.Flag == ChatListFolderFlags.IncludeContacts) recipients.SelectContacts = true;
                    if (flag.Flag == ChatListFolderFlags.IncludeNonContacts) recipients.SelectNonContacts = true;
                    if (flag.Flag == ChatListFolderFlags.ExistingChats) recipients.SelectExistingChats = true;
                    if (flag.Flag == ChatListFolderFlags.NewChats) recipients.SelectNewChats = true;
                }
                else if (item is FolderChat chat)
                {
                    recipients.ChatIds.Add(chat.ChatId);
                }
            }

            return recipients;
        }
    }
}
