using System;
using System.Collections.Generic;
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
    }
}
