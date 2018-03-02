using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Entities;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Views
{
    public class MediaLibraryCollection : IncrementalCollection<StorageMedia>, ISupportIncrementalLoading
    {
        private readonly CoreDispatcher _dispatcher;
        private readonly DisposableMutex _loadMoreLock;

        private StorageFileQueryResult _query;
        private uint _startIndex;

        private MediaLibraryCollection()
        {
            _dispatcher = Window.Current.Dispatcher;
            _loadMoreLock = new DisposableMutex();
        }

        private int _selectedCount;
        public int SelectedCount
        {
            get
            {
                return _selectedCount;
            }
        }

        private static Dictionary<int, WeakReference<MediaLibraryCollection>> _windowContext = new Dictionary<int, WeakReference<MediaLibraryCollection>>();
        public static MediaLibraryCollection GetForCurrentView()
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<MediaLibraryCollection> reference) && reference.TryGetTarget(out MediaLibraryCollection value))
            {
                return value;
            }

            var context = new MediaLibraryCollection();
            _windowContext[id] = new WeakReference<MediaLibraryCollection>(context);

            return context;
        }

        private async void OnContentsChanged(IStorageQueryResultBase sender, object args)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _startIndex = 0;
                Clear();
                UpdateCount();
            });
        }

        public override Task<IList<StorageMedia>> LoadDataAsync()
        {
            return Task.Run<IList<StorageMedia>>(async () =>
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    if (_query == null)
                    {
                        await KnownFolders.PicturesLibrary.TryGetItemAsync("yolo");

                        var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, Constants.MediaTypes);
                        queryOptions.FolderDepth = FolderDepth.Deep;

                        _query = KnownFolders.PicturesLibrary.CreateFileQueryWithOptions(queryOptions);
                        _query.ContentsChanged += OnContentsChanged;
                        _startIndex = 0;
                    }

                    var items = new List<StorageMedia>();
                    uint resultCount = 0;
                    var result = await _query.GetFilesAsync(_startIndex, 10);
                    _startIndex += (uint)result.Count;

                    resultCount = (uint)result.Count;

                    foreach (var file in result)
                    {
                        var storage = await StorageMedia.CreateAsync(file, false);
                        if (storage != null)
                        {
                            items.Add(storage);
                            storage.PropertyChanged += OnPropertyChanged;
                        }
                    }

                    return items;
                }
            });
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsSelected"))
            {
                UpdateCount();
            }
        }

        private void UpdateCount()
        {
            _selectedCount = this.Count(x => x.IsSelected);
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCount"));
        }
    }

    //public class MediaLibraryCollection : IncrementalCollection<StorageMedia>, ISupportIncrementalLoading
    //{
    //    public StorageLibrary Library => _library;
    //    public StorageFileQueryResult Query => _query;

    //    private readonly StorageMediaComparer _comparer;

    //    private StorageLibrary _library;
    //    private StorageFileQueryResult _query;

    //    private uint _startIndex;

    //    public MediaLibraryCollection()
    //    {
    //        if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
    //        {
    //            return;
    //        }

    //        _comparer = new StorageMediaComparer();
    //    }

    //    private int _selectedCount;
    //    public int SelectedCount
    //    {
    //        get
    //        {
    //            return _selectedCount;
    //        }
    //    }

    //    private async void OnContentsChanged(IStorageQueryResultBase sender, object args)
    //    {
    //        var reader = _library.ChangeTracker.GetChangeReader();
    //        var changes = await reader.ReadBatchAsync();

    //        foreach (StorageLibraryChange change in changes)
    //        {
    //            if (change.ChangeType == StorageLibraryChangeType.ChangeTrackingLost)
    //            {
    //                // Change tracker is in an invalid state and must be reset
    //                // This should be a very rare case, but must be handled
    //                Library.ChangeTracker.Reset();
    //                return;
    //            }
    //            if (change.IsOfType(StorageItemTypes.File))
    //            {
    //                await ProcessFileChange(change);
    //            }
    //            else if (change.IsOfType(StorageItemTypes.Folder))
    //            {
    //                // No-op; not interested in folders
    //            }
    //            else
    //            {
    //                if (change.ChangeType == StorageLibraryChangeType.Deleted)
    //                {
    //                    //UnknownItemRemoved(change.Path);
    //                }
    //            }
    //        }

    //        // Mark that all the changes have been seen and for the change tracker
    //        // to never return these changes again
    //        await reader.AcceptChangesAsync();
    //    }

    //    private async Task ProcessFileChange(StorageLibraryChange change)
    //    {
    //        switch (change.ChangeType)
    //        {
    //            // New File in the Library
    //            case StorageLibraryChangeType.Created:
    //            case StorageLibraryChangeType.MovedIntoLibrary:
    //            case StorageLibraryChangeType.MovedOrRenamed:
    //                if (Constants.MediaTypes.Any(x => change.Path.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
    //                {
    //                    var file = (StorageFile)(await change.GetStorageItemAsync());

    //                    Execute.BeginOnUIThread(async () =>
    //                    {
    //                        var storage = await StorageMedia.CreateAsync(file, false);
    //                        if (storage != null)
    //                        {
    //                            var array = this.ToArray();
    //                            var index = Array.BinarySearch(array, storage, _comparer);
    //                            if (index < 0) index = ~index;

    //                            // Insert only if newer than the last item
    //                            if (index < array.Length || !HasMoreItems)
    //                            {
    //                                _startIndex++;

    //                                Insert(index, storage);
    //                                storage.PropertyChanged += OnPropertyChanged;
    //                            }
    //                        }
    //                    });
    //                }
    //                break;
    //            // File Removed From Library
    //            case StorageLibraryChangeType.Deleted:
    //            case StorageLibraryChangeType.MovedOutOfLibrary:
    //                if (Constants.MediaTypes.Any(x => change.Path.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
    //                {
    //                    Execute.BeginOnUIThread(() =>
    //                    {
    //                        var already = this.FirstOrDefault(x => x.File.Path.Equals(change.Path));
    //                        if (already != null)
    //                        {
    //                            _startIndex--;

    //                            Remove(already);
    //                            UpdateSelected();
    //                        }
    //                    });
    //                }
    //                break;
    //            // Modified Contents
    //            case StorageLibraryChangeType.ContentsChanged:
    //                if (Constants.MediaTypes.Any(x => change.Path.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
    //                {
    //                    var file = (StorageFile)(await change.GetStorageItemAsync());

    //                    // Update thumbnail maybe
    //                }
    //                break;
    //            // Ignored Cases
    //            case StorageLibraryChangeType.EncryptionChanged:
    //            case StorageLibraryChangeType.ContentsReplaced:
    //            case StorageLibraryChangeType.IndexingStatusChanged:
    //            default:
    //                // These are safe to ignore in this application
    //                break;
    //        }
    //    }

    //    public override async Task<IList<StorageMedia>> LoadDataAsync()
    //    {
    //        if (_library == null)
    //        {
    //            _library = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
    //            _library.ChangeTracker.Enable();

    //            var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, Constants.MediaTypes);
    //            queryOptions.FolderDepth = FolderDepth.Deep;

    //            _query = KnownFolders.PicturesLibrary.CreateFileQueryWithOptions(queryOptions);
    //            _query.ContentsChanged += OnContentsChanged;
    //        }

    //        var items = new List<StorageMedia>();
    //        var result = await _query.GetFilesAsync(_startIndex, 10);

    //        _startIndex += (uint)result.Count;

    //        foreach (var file in result)
    //        {
    //            var storage = await StorageMedia.CreateAsync(file, false);
    //            if (storage != null)
    //            {
    //                items.Add(storage);
    //                storage.PropertyChanged += OnPropertyChanged;
    //            }
    //        }

    //        return items;
    //    }

    //    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    //    {
    //        if (e.PropertyName.Equals("IsSelected"))
    //        {
    //            UpdateSelected();
    //        }
    //    }

    //    private void UpdateSelected()
    //    {
    //        _selectedCount = this.Count(x => x.IsSelected);
    //        OnPropertyChanged(new PropertyChangedEventArgs("SelectedCount"));
    //    }

    //    class StorageMediaComparer : IComparer<StorageMedia>
    //    {
    //        public int Compare(StorageMedia x, StorageMedia y)
    //        {
    //            return y.Basic.ItemDate.CompareTo(x.Basic.ItemDate);
    //        }
    //    }
    //}
}
