using System;
using Unigram.ViewModels;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class AboutPage : Page
    {
        public AboutViewModel ViewModel => DataContext as AboutViewModel;

        public AboutPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<AboutViewModel>();
        }
    }
}
