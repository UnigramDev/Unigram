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
        private double _uploadingProgress;
        public double UploadingProgress
        {
            get
            {
                return _uploadingProgress;
            }
            set
            {
                _uploadingProgress = value;
                RaisePropertyChanged(() => UploadingProgress);
                RaisePropertyChanged(() => Progress);
            }
        }

        private double _downloadingProgress;
        public double DownloadingProgress
        {
            get
            {
                return _downloadingProgress;
            }
            set
            {
                _downloadingProgress = value;
                RaisePropertyChanged(() => DownloadingProgress);
                RaisePropertyChanged(() => Progress);
            }
        }

        public double Progress
        {
            get
            {
                if (_downloadingProgress > 0)
                {
                    return _downloadingProgress;
                }

                return _uploadingProgress;
            }
        }

        public double LastProgress { get; set; }

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

    public partial class TLMessageMediaContact
    {
        private TLUser _user;
        public TLUser User
        {
            get
            {
                if (_user == null)
                {
                    var user = InMemoryCacheService.Current.GetUser(UserId) as TLUser;
                    if (user == null)
                    {
                        user = new TLUser
                        {
                            FirstName = FirstName,
                            LastName = LastName,
                            Id = UserId,
                            Phone = PhoneNumber,
                            Photo = new TLUserProfilePhotoEmpty()
                        };
                    }

                    _user = user;
                }

                return _user;
            }
        }
    }
}
