using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;

namespace Telegram.Api.TL
{
    public partial class TLPhotoBase : ITLTransferable, INotifyPropertyChanged
    {
        public virtual TLInputPhotoBase ToInputPhoto()
        {
            throw new NotImplementedException();
        }

        #region Download/upload

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

        private bool _isTransferring;
        public bool IsTransferring
        {
            get
            {
                return _isTransferring;
            }
            set
            {
                if (_isTransferring != value)
                {
                    _isTransferring = value;
                    RaisePropertyChanged(() => IsTransferring);
                }
            }
        }

        public double LastProgress { get; set; }

        public Progress<double> Download()
        {
            IsTransferring = true;

            return new Progress<double>((value) =>
            {
                DownloadingProgress = value;
                IsTransferring = value < 1 && value > 0;
                Debug.WriteLine(value);
            });
        }

        public Progress<double> Upload()
        {
            IsTransferring = true;

            return new Progress<double>((value) =>
            {
                UploadingProgress = value;
                IsTransferring = value < 1 && value > 0;
                Debug.WriteLine(value);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }

        #endregion
    }

    public partial class TLPhoto
    {
        public async void DownloadAsync(IDownloadFileManager manager, Action<TLPhoto> completed)
        {
            var photoSize = Full as TLPhotoSize;
            if (photoSize == null)
            {
                return;
            }

            var location = photoSize.Location as TLFileLocation;
            if (location == null)
            {
                return;
            }

            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {

            }
            else
            {
                if (IsTransferring)
                {
                    return;
                }

                IsTransferring = true;

                var operation = manager.DownloadFileAsync(location, photoSize.Size);
                var download = await operation.AsTask(Download());
                if (download != null)
                {
                    UploadingProgress = 0;
                    DownloadingProgress = 1;
                    IsTransferring = false;
                    completed(this);
                }
            }
        }
        public void Cancel(IDownloadFileManager manager, IUploadManager uploadManager)
        {
            if (manager != null)
            {
                manager.CancelDownloadFile(this);
                DownloadingProgress = 0;
                IsTransferring = false;
            }

            if (uploadManager != null)
            {
                uploadManager.CancelUploadFile(Id);
                UploadingProgress = 0;
                IsTransferring = false;
            }
        }

        public override TLInputPhotoBase ToInputPhoto()
        {
            return new TLInputPhoto { Id = Id, AccessHash = AccessHash };
        }
    }

    public partial class TLPhotoEmpty
    {
        public override TLInputPhotoBase ToInputPhoto()
        {
            return new TLInputPhotoEmpty();
        }
    }
}
