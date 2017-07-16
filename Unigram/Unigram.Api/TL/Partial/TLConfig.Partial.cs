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

        //public override void ReadFromCache(TLBinaryReader from)
        //{
        //    ActiveDCOptionIndex = from.ReadInt32();
        //    Country = from.ReadString();
        //    LastUpdate = DateTime.FromBinary(from.ReadInt64());
        //}

        //public override void WriteToCache(TLBinaryWriter to)
        //{
        //    to.Write(ActiveDCOptionIndex);
        //    to.Write(Country);
        //    to.Write(LastUpdate.ToBinary());
        //}
    }
}
