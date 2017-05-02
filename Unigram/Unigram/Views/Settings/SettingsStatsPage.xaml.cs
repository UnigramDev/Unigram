using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStatsPage : Page
    {
        public SettingsStatsViewModel ViewModel => DataContext as SettingsStatsViewModel;

        public SettingsStatsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsStatsViewModel>();
        }
    }
}
