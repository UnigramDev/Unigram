using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;

namespace Telegram.Api.TL
{
    public abstract partial class TLMessageMediaBase : INotifyPropertyChanged
    {
        public virtual double UploadingProgress { get; set; }
        public virtual double DownloadingProgress { get; set; }
        public virtual double LastProgress { get; set; }

        public double Progress
        {
            get
            {
                if (DownloadingProgress > 0)
                {
                    return DownloadingProgress;
                }

                return UploadingProgress;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }

        public Progress<double> Download()
        {
            DownloadingProgress = 0.02;

            return new Progress<double>((value) =>
            {
                DownloadingProgress = value;
                Debug.WriteLine(value);
            });
        }

        public Progress<double> Upload()
        {
            UploadingProgress = 0.02;

            return new Progress<double>((value) =>
            {
                UploadingProgress = value;
                Debug.WriteLine(value);
            });
        }
    }
}
