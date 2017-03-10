using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Core.Dependency;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DialogSharedMediaPage : Page
    {
        public DialogSharedMediaViewModel ViewModel => DataContext as DialogSharedMediaViewModel;

        public DialogSharedMediaPage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<DialogSharedMediaViewModel>();

            // Used to get semi-transparent background for headers.
            // TODO: Check for performance issues on mobile.
            ScrollingMedia.Loaded += Host_Loaded;
            ScrollingFiles.Loaded += Host_Loaded;
            ScrollingMusic.Loaded += Host_Loaded;
        }

        private void Host_Loaded(object sender, RoutedEventArgs e)
        {
            var list = sender as ListViewBase;
            if (list != null)
            {
                if (list.ItemsPanelRoot != null)
                {
                    list.ItemsPanelRoot.RegisterPropertyChangedCallback(ClipProperty, new DependencyPropertyChangedCallback((s, dp) => list.ItemsPanelRoot.Clip = null));
                }
            }
        }
    }
}
