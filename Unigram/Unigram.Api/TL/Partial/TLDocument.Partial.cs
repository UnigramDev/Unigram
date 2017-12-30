using System;
using System.Collections.Generic;
using System.Globalization;
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
        public async void DownloadAsync(IDownloadManager manager, Action<TLDocument> completed)
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

                var download = await manager.DownloadFileAsync(FileName, DCId, ToInputFileLocation(), Size, Download());
                if (download != null)
                {
                    //UploadingProgress = 0;
                    //DownloadingProgress = 0;
                    completed(this);
                }
            }
        }

        public void Cancel(IDownloadManager manager, IUploadManager uploadManager)
        {
            if (manager != null)
            {
                manager.Cancel(this);
            }

            if (uploadManager != null)
            {
                uploadManager.Cancel(UploadId ?? 0);
            }

            UploadingProgress = 0;
            DownloadingProgress = 0;
        }

        private string _fileName;
        public string FileName
        {
            get
            {
                if (_fileName == null)
                {
                    var attribute = Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                    if (attribute != null)
                    {
                        _fileName = string.Join("_", attribute.FileName.Split(Path.GetInvalidFileNameChars())).Replace("\u0085", string.Empty);
                        return _fileName;
                    }

                    var videoAttribute = Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                    if (videoAttribute != null)
                    {
                        _fileName = "Video.mp4";
                        return _fileName;
                    }

                    var audioAttribute = Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                    if (audioAttribute != null)
                    {
                        _fileName = "Audio.ogg";
                        return _fileName;
                    }

                    _fileName = "File.dat";
                }

                return _fileName;
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
                var attribute = Attributes.FirstOrDefault(x => x is TLDocumentAttributeSticker) as TLDocumentAttributeSticker;
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
                var attribute = Attributes.FirstOrDefault(x => x is TLDocumentAttributeSticker) as TLDocumentAttributeSticker;
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

        public string Info
        {
            get
            {
                var info = string.Empty;
                var animated = Attributes.FirstOrDefault(x => x is TLDocumentAttributeAnimated);
                if (animated != null)
                {
                    info = "GIF";
                }
                else
                {
                    info = Duration;
                }

                if (info.Length > 0)
                {
                    info += ", ";
                }

                var bytesCount = Size;
                if (bytesCount < 1024L)
                {
                    return string.Format("{1}{0} B", bytesCount, info);
                }
                if (bytesCount < 1048576L)
                {
                    return string.Format("{1}{0} KB", ((double)bytesCount / 1024.0).ToString("0.0", CultureInfo.InvariantCulture), info);
                }
                if (bytesCount < 1073741824L)
                {
                    return string.Format("{1}{0} MB", ((double)bytesCount / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture), info);
                }

                return string.Format("{1}{0} GB", ((double)bytesCount / 1024.0 / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture), info);
            }
        }

        public string GetFileName()
        {
            if (Version > 0)
            {
                return string.Format("document{0}_{1}{2}", Id, Version, GetFileExtension());
            }

            return string.Format("document{0}_{1}{2}", Id, AccessHash, GetFileExtension());
        }

        public string GetFileExtension()
        {
            return Path.GetExtension(FileName);

            var attribute = Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
            if (attribute != null)
            {
                return Path.GetExtension(string.Join("_", attribute.FileName.Split(Path.GetInvalidFileNameChars())));
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
