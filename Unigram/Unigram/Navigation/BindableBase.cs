using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Unigram.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-MVVM
    public abstract class BindableBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual bool Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        public virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            var handler = PropertyChanged;
            if (!Equals(handler, null))
            {
                try
                {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
                catch { }
            }
        }
    }
}
