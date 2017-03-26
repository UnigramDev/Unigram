using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Common
{
    public class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        private bool _suspendUpdates;

        public ObservableCollectionEx() { }
        public ObservableCollectionEx(IEnumerable<T> collection) : base(collection) { }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suspendUpdates)
            {
                return;
            }

            base.OnCollectionChanged(e);
        }

        public void AddRange(IEnumerable<T> source, bool clear = false)
        {
            _suspendUpdates = true;

            if (clear)
            {
                Clear();
            }

            foreach (var item in source)
            {
                Add(item);
            }
            _suspendUpdates = false;

            if (clear)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            else
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, Items));
            }
        }
    }
}
