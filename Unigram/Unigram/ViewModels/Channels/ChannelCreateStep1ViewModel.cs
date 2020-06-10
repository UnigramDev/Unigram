using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Channels;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.ViewModels.Channels
{
    public class ChannelCreateStep1ViewModel : TLViewModelBase
    {
        public ChannelCreateStep1ViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !string.IsNullOrWhiteSpace(Title));
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                Set(ref _title, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private string _about;
        public string About
        {
            get
            {
                return _about;
            }
            set
            {
                Set(ref _about, value);
            }
        }

        private BitmapImage _preview;
        public BitmapImage Preview
        {
            get
            {
                return _preview;
            }
            set
            {
                Set(ref _preview, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await ProtoService.SendAsync(new CreateNewSupergroupChat(_title, true, _about ?? string.Empty, null));
            if (response is Chat chat)
            {
                // TODO: photo

                NavigationService.Navigate(typeof(ChannelCreateStep2Page), chat.Id);
                NavigationService.RemoveLast();
                NavigationService.RemoveLast();
            }
        }

        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
        }
    }
}
