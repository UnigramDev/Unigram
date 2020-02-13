using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Filters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Filters
{
    public sealed partial class FiltersPage : Page
    {
        public FiltersViewModel ViewModel => DataContext as FiltersViewModel;

        public FiltersPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<FiltersViewModel>();
        }
    }
}
