using Rg.DiffUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Settings;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class DownloadsViewModel : TLViewModelBase, IHandle
        //IHandle<UpdateFileDownload>,
        //IHandle<UpdateFileAddedToDownloads>,
        //IHandle<UpdateFileRemovedFromDownloads>
    {
        private readonly IStorageService _storageService;

        public DownloadsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _storageService = storageService;

            //Items = new ItemCollection(this, string.Empty);
            Items = new SearchCollection<FileDownloadViewModel, ItemCollection>(SetSearch, new FileDownloadDiffHandler());
            Items.SetQuery(string.Empty);

            RemoveAllCommand = new RelayCommand(RemoveAll);
            ToggleAllPausedCommand = new RelayCommand(ToggleAllPaused);
            SettingsCommand = new RelayCommand(Settings);

            RemoveFileDownloadCommand = new RelayCommand<FileDownloadViewModel>(RemoveFileDownload);
            ViewFileDownloadCommand = new RelayCommand<FileDownloadViewModel>(ViewFileDownload);
            ShowFileDownloadCommand = new RelayCommand<FileDownloadViewModel>(ShowFileDownload);
        }

        public Action Hide { get; set; }

        public SearchCollection<FileDownloadViewModel, ItemCollection> Items { get; private set; }

        private ItemCollection SetSearch(object sender, string query)
        {
            return new ItemCollection(this, query);
        }

        private bool _isEmpty = false;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        private int _totalCompletedCount;
        public int TotalCompletedCount
        {
            get => _totalCompletedCount;
            set => Set(ref _totalCompletedCount, value);
        }

        private int _totalPausedCount;
        public int TotalPausedCount
        {
            get => _totalPausedCount;
            set => Set(ref _totalPausedCount, value);
        }

        private int _totalActiveCount;
        public int TotalActiveCount
        {
            get => _totalActiveCount;
            set => Set(ref _totalActiveCount, value);
        }

        public void Handle(UpdateFileDownload update)
        {
            if (Items.Source.TryGetValue(update.FileId, out FileDownloadViewModel fileDownload))
            {
                Dispatcher.Dispatch(() =>
                {
                    if (update.CompleteDate != 0 && update.CompleteDate != fileDownload.CompleteDate)
                    {
                        var first = Items.FirstOrDefault(x => x.IsFirst && x.CompleteDate != 0);

                        var next = Items.IndexOf(first);
                        var prev = Items.IndexOf(fileDownload);

                        if (prev == 0 && next > 1)
                        {
                            Items[1].IsFirst = true;
                        }

                        // If the future position is after the current(supposedly
                        // it's always the case) we have to decrease the index
                        // otherwise the item will move after the one.
                        if (next > prev)
                        {
                            next--;
                        }

                        if (next != prev)
                        {
                            Items.Remove(fileDownload);
                            Items.Insert(next >= 0 ? next : Items.Count, fileDownload);
                        }

                        if (first != null)
                        {
                            first.IsFirst = false;
                        }

                        fileDownload.IsFirst = true;
                    }

                    fileDownload.CompleteDate = update.CompleteDate;
                    fileDownload.IsPaused = update.IsPaused;

                    Items.Source.UpdateProperties(update.Counts);
                });
            }
        }

        public void Handle(UpdateFileAddedToDownloads update)
        {
            Dispatcher.Dispatch(() => Items.Source.UpdateProperties(update.Counts));
        }

        public void Handle(UpdateFileRemovedFromDownloads update)
        {
            Dispatcher.Dispatch(() => Items.Source.RemoveById(update));
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateFileDownload>(this, Handle)
                .Subscribe<UpdateFileAddedToDownloads>(Handle)
                .Subscribe<UpdateFileRemovedFromDownloads>(Handle);
        }

        public RelayCommand RemoveAllCommand { get; }
        private void RemoveAll()
        {
            ProtoService.Send(new RemoveAllFilesFromDownloads(false, false, true));
        }

        public RelayCommand ToggleAllPausedCommand { get; }
        private void ToggleAllPaused()
        {
            ProtoService.Send(new ToggleAllDownloadsArePaused(_totalActiveCount > 0));
        }

        public RelayCommand SettingsCommand { get; }
        private void Settings()
        {
            Hide();
            NavigationService.Navigate(typeof(SettingsStoragePage));
        }

        public RelayCommand<FileDownloadViewModel> RemoveFileDownloadCommand { get; }
        private void RemoveFileDownload(FileDownloadViewModel fileDownload)
        {
            ProtoService.Send(new RemoveFileFromDownloads(fileDownload.FileId, true));
        }

        public RelayCommand<FileDownloadViewModel> ViewFileDownloadCommand { get; }
        private void ViewFileDownload(FileDownloadViewModel fileDownload)
        {
            Hide();
            NavigationService.NavigateToChat(fileDownload.Message.ChatId, message: fileDownload.Message.Id);
        }

        public RelayCommand<FileDownloadViewModel> ShowFileDownloadCommand { get; }
        private async void ShowFileDownload(FileDownloadViewModel fileDownload)
        {
            var file = await ProtoService.SendAsync(new GetFile(fileDownload.FileId)) as File;
            if (file != null)
            {
                await _storageService.OpenFolderAsync(file);
            }
        }

        public class ItemCollection : ObservableCollection<FileDownloadViewModel>, ISupportIncrementalLoading
        {
            private readonly ConcurrentDictionary<int, FileDownloadViewModel> _items = new();

            private readonly DownloadsViewModel _viewModel;

            private readonly string _query;
            private bool _onlyActive;
            private bool _onlyCompleted;

            private string _offset = string.Empty;
            private bool _hasMoreItems = true;

            private bool _first = true;

            public ItemCollection(DownloadsViewModel viewModel, string query)
            {
                _viewModel = viewModel;

                _query = query ?? string.Empty;
                _onlyActive = true;
                _onlyCompleted = false;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _viewModel.ProtoService.SendAsync(new SearchFileDownloads(_query, _onlyActive, _onlyCompleted, _offset, 100));
                    if (response is FoundFileDownloads found)
                    {
                        foreach (var file in found.Files)
                        {
                            var item = new FileDownloadViewModel(file)
                            {
                                IsFirst = _first
                            };

                            _first = false;
                            _items[item.FileId] = item;

                            Add(item);
                        }

                        if (string.IsNullOrEmpty(found.NextOffset))
                        {
                            _offset = found.NextOffset;
                            _hasMoreItems = _onlyActive;

                            _onlyActive = false;
                            _onlyCompleted = true;

                            _first = true;

                            if (_hasMoreItems)
                            {
                                return await LoadMoreItemsAsync(count);
                            }
                        }
                        else
                        {
                            _offset = found.NextOffset;
                        }

                        UpdateProperties(found.TotalCounts);
                        return new LoadMoreItemsResult { Count = (uint)found.Files.Count };
                    }

                    _offset = string.Empty;
                    _hasMoreItems = false;

                    UpdateProperties(null);
                    return new LoadMoreItemsResult { Count = 0 };
                });
            }

            public void UpdateProperties(DownloadedFileCounts counts)
            {
                _viewModel.TotalCompletedCount = counts?.CompletedCount ?? 0;
                _viewModel.TotalPausedCount = counts?.PausedCount ?? 0;
                _viewModel.TotalActiveCount = counts?.ActiveCount ?? 0;

                _viewModel.IsEmpty = _items.Count == 0;
            }

            public bool HasMoreItems => _hasMoreItems;

            public void RemoveById(UpdateFileRemovedFromDownloads update)
            {
                if (_items.TryRemove(update.FileId, out FileDownloadViewModel fileDownload))
                {
                    Remove(fileDownload);
                }

                UpdateProperties(update.Counts);
            }

            public bool TryGetValue(int fileId, out FileDownloadViewModel fileDownload)
            {
                return _items.TryGetValue(fileId, out fileDownload);
            }
        }
    }

    public class FileDownloadViewModel : BindableBase
    {
        private readonly FileDownload _fileDownload;

        public FileDownloadViewModel(FileDownload fileDownload)
        {
            _fileDownload = fileDownload;
        }

        private bool _isFirst;
        public bool IsFirst
        {
            get => _isFirst;
            set => Set(ref _isFirst, value);
        }

        /// <summary>
        /// True, if downloading of the file is paused.
        /// </summary>
        public bool IsPaused { get => _fileDownload.IsPaused; set => _fileDownload.IsPaused = value; }

        /// <summary>
        /// Point in time (Unix timestamp) when the file downloading was completed; 0 if
        /// the file downloading isn't completed.
        /// </summary>
        public int CompleteDate
        {
            get => _fileDownload.CompleteDate;
            set
            {
                _fileDownload.CompleteDate = value;
                RaisePropertyChanged(nameof(CompleteDate));
            }
        }

        /// <summary>
        /// Point in time (Unix timestamp) when the file was added to the download list.
        /// </summary>
        public int AddDate => _fileDownload.AddDate;

        /// <summary>
        /// The message with the file.
        /// </summary>
        public Message Message => _fileDownload.Message;

        /// <summary>
        /// File identifier.
        /// </summary>
        public int FileId => _fileDownload.FileId;
    }

    public class FileDownloadDiffHandler : IDiffHandler<FileDownloadViewModel>
    {
        public bool CompareItems(FileDownloadViewModel oldItem, FileDownloadViewModel newItem)
        {
            return oldItem?.FileId == newItem?.FileId;
        }

        public void UpdateItem(FileDownloadViewModel oldItem, FileDownloadViewModel newItem)
        {
            oldItem.IsFirst = newItem.IsFirst;
        }
    }
}
