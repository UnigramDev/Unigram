using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessageMediaContact
    {
        public string FullName
        {
            get
            {
                return string.Format("{0} {1}", FirstName, LastName).Trim();
            }
        }
    }
}
