using System;
using Unigram.Converters;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsDataAutoPage : HostedPage
    {
        public SettingsDataAutoViewModel ViewModel => DataContext as SettingsDataAutoViewModel;

        public SettingsDataAutoPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsDataAutoViewModel>();
        }

        #region Binding

        private double ConvertLimit(int size)
        {
            var progress = 0.0d;
            size -= 500 * 1024;
            if (size < 524 * 1024)
            {
                progress = Math.Max(0, size / (double)(524 * 1024)) * 0.25f;
            }
            else
            {
                progress += 0.25f;
                size -= 524 * 1024;

                if (size < 1024 * 1024 * 9)
                {
                    progress += Math.Max(0, size / (double)(9 * 1024 * 1024)) * 0.25f;
                }
                else
                {
                    progress += 0.25f;
                    size -= 9 * 1024 * 1024;

                    if (size < 1024 * 1024 * 90)
                    {
                        progress += Math.Max(0, size / (double)(90 * 1024 * 1024)) * 0.25f;
                    }
                    else
                    {
                        progress += 0.25f;
                        size -= 90 * 1024 * 1024;

                        progress += Math.Max(0, size / (double)(1436 * 1024 * 1024)) * 0.25f;
                    }
                }
            }

            return progress;
        }

        private void ConvertLimitBack(double progress)
        {
            int size = 500 * 1024;
            if (progress <= 0.25f)
            {
                size += (int)(524 * 1024 * (progress / 0.25f));
            }
            else
            {
                progress -= 0.25f;
                size += 524 * 1024;

                if (progress < 0.25f)
                {
                    size += (int)(9 * 1024 * 1024 * (progress / 0.25f));
                }
                else
                {
                    progress -= 0.25f;
                    size += 9 * 1024 * 1024;

                    if (progress <= 0.25f)
                    {
                        size += (int)(90 * 1024 * 1024 * (progress / 0.25f));
                    }
                    else
                    {
                        progress -= 0.25f;
                        size += 90 * 1024 * 1024;

                        size += (int)(1436 * 1024 * 1024 * (progress / 0.25f));
                    }
                }
            }

            ViewModel.Limit = size;
        }

        private string ConvertUpTo(int limit)
        {
            return FileSizeConverter.Convert(limit, true);
        }

        #endregion

    }
}
