using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLDocument
    {
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

        public string Duration
        {
            get
            {
                var videoAttribute = Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                if (videoAttribute != null)
                {
                    return TimeSpan.FromSeconds(videoAttribute.Duration).ToString("mm\\:ss");
                }

                var audioAttribute = Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                if (audioAttribute != null)
                {
                    return TimeSpan.FromSeconds(audioAttribute.Duration).ToString("mm\\:ss");
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
