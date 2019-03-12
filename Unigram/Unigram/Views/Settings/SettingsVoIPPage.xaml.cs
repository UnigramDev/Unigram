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
    public sealed partial class SettingsVoIPPage : Page
    {
        public SettingsVoIPViewModel ViewModel => DataContext as SettingsVoIPViewModel;

        public SettingsVoIPPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsVoIPViewModel>();
        }

        #region Binding

        private string ConvertVolumeLabel(float value, bool output)
        {
            if (output)
            {
                return string.Format("Output volume: {0}", (int)(value * 100));
            }

            return string.Format("Input volume: {0}", (int)(value * 100));
        }

        private double ConvertVolume(float value)
        {
            return (double)value * 100d;
        }

        private void ConvertVolumeOutput(double value)
        {
            ViewModel.OutputVolume = (float)value / 100f;
        }

        private void ConvertVolumeInput(double value)
        {
            ViewModel.InputVolume = (float)value / 100f;
        }

        #endregion

    }
}
