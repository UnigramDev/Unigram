using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsVoIPPage : HostedPage
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
