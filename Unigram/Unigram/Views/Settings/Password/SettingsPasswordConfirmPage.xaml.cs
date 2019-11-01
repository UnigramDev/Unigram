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
    public sealed partial class SettingsPasswordConfirmPage : Page
    {
        public SettingsPasswordConfirmViewModel ViewModel => DataContext as SettingsPasswordConfirmViewModel;

        public SettingsPasswordConfirmPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordConfirmViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        #region Binding

        private string ConvertPattern(string pattern)
        {
            return string.Format("[Please enter code we've just emailed at **{0}**]", pattern);
        }

        #endregion

    }
}
