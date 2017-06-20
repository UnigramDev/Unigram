using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using System.Threading.Tasks;

namespace Unigram.Views
{
    public sealed partial class DialogSharedMediaPage : Page
    {
        public DialogSharedMediaViewModel ViewModel => DataContext as DialogSharedMediaViewModel;

        public DialogSharedMediaPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<DialogSharedMediaViewModel>();
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            Themes.Media.Photo_Click(sender);
        }
    }
}
