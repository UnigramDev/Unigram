using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLConfig
    {
        [DataMember]
        public Int32 ActiveDCOptionIndex { get; set; }

        [DataMember]
        public String Country { get; set; }

        [DataMember]
        public DateTime LastUpdate { get; set; }
    }
}
