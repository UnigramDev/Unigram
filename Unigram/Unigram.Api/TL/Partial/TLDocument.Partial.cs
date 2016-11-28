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

                return "Resources.Document";
            }
        }

        public string GetFileName()
        {
            return string.Format("document{0}_{1}{2}", new object[]
            {
                Id,
                AccessHash,
                Path.GetExtension(FileName)
            });
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
