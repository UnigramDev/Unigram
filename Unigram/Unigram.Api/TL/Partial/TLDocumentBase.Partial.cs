using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLDocumentBase : ITLTransferable, INotifyPropertyChanged
    {
        public long? UploadId { get; set; }

		public virtual TLInputDocumentBase ToInputDocument()
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
                _downloadingProgress = 0;
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
                _uploadingProgress = 0;
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

    public partial class TLDocument
    {
        public override TLInputDocumentBase ToInputDocument()
        {
            return new TLInputDocument { Id = Id, AccessHash = AccessHash };
        }
    }

	public partial class TLDocumentEmpty
    {
        public override TLInputDocumentBase ToInputDocument()
        {
            return new TLInputDocumentEmpty();
        }
    }
}
