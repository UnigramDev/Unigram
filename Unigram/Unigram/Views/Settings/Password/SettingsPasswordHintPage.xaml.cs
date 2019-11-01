using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.ViewModels.Settings.Password;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordHintPage : Page
    {
        public SettingsPasswordHintViewModel ViewModel => DataContext as SettingsPasswordHintViewModel;

        public SettingsPasswordHintPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordHintViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }
    }
}
