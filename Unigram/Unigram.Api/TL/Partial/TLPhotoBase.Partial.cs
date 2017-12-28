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
        public long? UploadId { get; set; }

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
                RaisePropertyChanged(() => IsTransferring);
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
                RaisePropertyChanged(() => IsTransferring);
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

        public bool IsTransferring
        {
            get
            {
                return (_downloadingProgress > 0 && _downloadingProgress < 1) || (_uploadingProgress > 0 && _uploadingProgress < 1);
            }
        }

        public double LastProgress { get; set; }

        public Progress<double> Download()
        {
            DownloadingProgress = double.Epsilon;

            return new Progress<double>((value) =>
            {
                DownloadingProgress = value;
                Debug.WriteLine(value);
            });
        }

        public Progress<double> Upload()
        {
            UploadingProgress = double.Epsilon;

            return new Progress<double>((value) =>
            {
                UploadingProgress = value;
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

                var download = await manager.DownloadFileAsync(location, photoSize.Size, Download());
                if (download != null)
                {
                    completed(this);
                }
            }
        }
        public void Cancel(IDownloadFileManager manager, IUploadManager uploadManager)
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

            if (manager != null)
            {
                manager.Cancel(location);
            }

            if (uploadManager != null)
            {
                uploadManager.Cancel(UploadId ?? 0);
            }

            DownloadingProgress = 0;
            UploadingProgress = 0;
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
