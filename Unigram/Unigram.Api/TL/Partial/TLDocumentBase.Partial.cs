using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLDocumentBase
    {
		public virtual TLInputDocumentBase ToInputDocument()
        {
            throw new NotImplementedException();
        }
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
