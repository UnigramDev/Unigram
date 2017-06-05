using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;

namespace Telegram.Api.TL
{
    public partial class TLDocument
    {
        public async void DownloadAsync(IDownloadManager manager)
        {
            var fileName = this.GetFileName();
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

                var operation = manager.DownloadFileAsync(FileName, DCId, ToInputFileLocation(), Size);
                var download = await operation.AsTask(Download());
                if (download != null)
                {
                    IsTransferring = false;
                }
            }
        }

        public void Cancel(IDownloadManager manager, IUploadManager uploadManager)
        {
            if (DownloadingProgress > 0 && DownloadingProgress < 1)
            {
                manager.CancelDownloadFile(this);
                DownloadingProgress = 0;
                IsTransferring = false;
            }
            else if (UploadingProgress > 0 && UploadingProgress < 1)
            {
                uploadManager.CancelUploadFile(Id);
                UploadingProgress = 0;
                IsTransferring = false;
            }
        }

        public string FileName
        {
            get
            {
                var attribute = Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                if (attribute != null)
                {
                    return attribute.FileName;
                }

                var videoAttribute = Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                if (videoAttribute != null)
                {
                    return "Video.mp4";
                }

                return "Resources.Document";
            }
        }

        public string Title
        {
            get
            {
                var audioAttribute = Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                if (audioAttribute != null)
                {
                    if (audioAttribute.HasPerformer && audioAttribute.HasTitle)
                    {
                        return $"{audioAttribute.Performer} - {audioAttribute.Title}";
                    }
                    else if (audioAttribute.HasPerformer && !audioAttribute.HasTitle)
                    {
                        return $"{audioAttribute.Performer} - Unknown Track";
                    }
                    else if (audioAttribute.HasTitle && !audioAttribute.HasPerformer)
                    {
                        return $"{audioAttribute.Title}";
                    }
                }

                return FileName;
            }
        }

        public string Emoticon
        {
            get
            {
                var attribute = Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (attribute != null && !attribute.IsMask)
                {
                    return attribute.Alt;
                }

                return string.Empty;
            }
        }

        public TLInputStickerSetBase StickerSet
        {
            get
            {
                var attribute = Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (attribute != null /* && !attribute.IsMask*/)
                {
                    return attribute.StickerSet;
                }

                return null;
            }
        }

        public string Duration
        {
            get
            {
                var videoAttribute = Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                if (videoAttribute != null)
                {
                    var duration = TimeSpan.FromSeconds(videoAttribute.Duration);
                    if (duration.TotalHours >= 1)
                    {
                        return duration.ToString("h\\:mm\\:ss");
                    }
                    else
                    {
                        return duration.ToString("mm\\:ss");
                    }
                }

                var audioAttribute = Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                if (audioAttribute != null)
                {
                    var duration = TimeSpan.FromSeconds(audioAttribute.Duration);
                    if (duration.TotalHours >= 1)
                    {
                        return duration.ToString("h\\:mm\\:ss");
                    }
                    else
                    {
                        return duration.ToString("mm\\:ss");
                    }
                }

                return null;
            }
        }

        public string GetFileName()
        {
            return string.Format("document{0}_{1}{2}", Id, AccessHash, GetFileExtension());
        }

        public string GetFileExtension()
        {
            var attribute = Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
            if (attribute != null)
            {
                return Path.GetExtension(attribute.FileName);
            }

            var videoAttribute = Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
            if (videoAttribute != null)
            {
                return ".mp4";
            }

            var audioAttribute = Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
            if (audioAttribute != null)
            {
                return ".ogg";
            }

            // TODO: mime conversion?

            return ".dat";
        }

        public TLInputDocumentFileLocation ToInputFileLocation()
        {
            return new TLInputDocumentFileLocation
            {
                AccessHash = AccessHash,
                Id = Id
            };
        }
    }
}
