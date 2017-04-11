using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Core.Common;

namespace Unigram.ViewModels
{
    public abstract class PhotosViewModelBase : UnigramViewModelBase
    {
        public PhotosViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
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

        protected object _selectedItem;
        public object SelectedItem
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

        public ObservableCollection<object> Items { get; protected set; }

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

        public RelayCommand DeleteCommand => new RelayCommand(DeleteExecute);
        protected virtual void DeleteExecute()
        {
        }
    }
}
