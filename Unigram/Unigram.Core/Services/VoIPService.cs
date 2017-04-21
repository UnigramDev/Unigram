using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.TL;

namespace Unigram.Core.Services
{
    public interface IVoIPService
    {

    }

    public class VoIPService : IVoIPService, IHandle<TLUpdatePhoneCall>, IHandle
    {
        public void Handle(TLUpdatePhoneCall update)
        {
            throw new NotImplementedException();
        }
    }
}
