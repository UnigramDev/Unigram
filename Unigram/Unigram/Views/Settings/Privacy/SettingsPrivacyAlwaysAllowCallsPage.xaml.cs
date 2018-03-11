using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Settings.Privacy;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyAlwaysAllowCallsPage : Page
    {
        public SettingsPrivacyAlwaysAllowCallsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.Resolve<SettingsPrivacyAlwaysAllowCallsViewModel>();
            View.Attach();
        }
    }
}
