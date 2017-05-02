using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Core.Common;

namespace Unigram.ViewModels
{
    public abstract class GalleryViewModelBase : UnigramViewModelBase
    {
        public GalleryViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public int SelectedIndex
        {
            get
            {
                if (Items == null || SelectedItem == null)
                {
                    return 0;
                }

                var index = Items.IndexOf(SelectedItem);
                if (index == Items.Count - 1)
                {
                    LoadNext();
                }
                if (index == 0)
                {
                    LoadPrevious();
                }

                return index + 1;
            }
        }

        protected int _totalItems;
        public int TotalItems
        {
            get
            {
                return _totalItems;
            }
            set
            {
                Set(ref _totalItems, value);
            }
        }

        protected GalleryItem _selectedItem;
        public GalleryItem SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                RaisePropertyChanged(() => SelectedIndex);
            }
        }

        protected object _poster;
        public object Poster
        {
            get
            {
                return _poster;
            }
            set
            {
                Set(ref _poster, value);
            }
        }

        public MvxObservableCollection<GalleryItem> Items { get; protected set; }

        protected virtual void LoadPrevious() { }

        protected virtual void LoadNext() { }

        public virtual bool CanDelete
        {
            get
            {
                return false;
            }
        }

        public virtual bool CanSave
        {
            get
            {
                return false;
            }
        }

        public RelayCommand StickersCommand => new RelayCommand(StickersExecute);
        private async void StickersExecute()
        {
            if (_selectedItem != null && _selectedItem.HasStickers)
            {
                var inputStickered = _selectedItem.ToInputStickeredMedia();
                if (inputStickered != null)
                {
                    var response = await ProtoService.GetAttachedStickersAsync(inputStickered);
                    if (response.IsSucceeded)
                    {
                        if (response.Result.Count > 1)
                        {
                            await AttachedStickersView.Current.ShowAsync(response.Result);
                        }
                        else if (response.Result.Count > 0)
                        {
                            await StickerSetView.Current.ShowAsync(response.Result[0]);
                        }
                    }
                }
            }
        }

        public RelayCommand DeleteCommand => new RelayCommand(DeleteExecute);
        protected virtual void DeleteExecute()
        {
        }
    }

    public class GalleryItem : BindableBase
    {
        public GalleryItem()
        {

        }

        public GalleryItem(object source, string caption, ITLDialogWith from, int date, bool stickers)
        {
            Source = source;
            Caption = caption;
            From = from;
            Date = date;
            HasStickers = stickers;
        }

        public virtual object Source { get; private set; }

        public virtual string Caption { get; private set; }

        public virtual ITLDialogWith From { get; private set; }

        public virtual int Date { get; private set; }

        public virtual bool IsVideo { get; private set; }

        public virtual bool HasStickers { get; private set; }

        public virtual TLInputStickeredMediaBase ToInputStickeredMedia()
        {
            throw new NotImplementedException();
        }

        public virtual Uri GetVideoSource()
        {
            throw new NotImplementedException();
        }
    }
}
