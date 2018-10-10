using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Unigram.Services.Updates
{
    public class UpdateCallDialog
    {
        public UpdateCallDialog(Call call, bool open)
        {
            IsOpen = open;
        }

        public Call Call { get; private set; }
        public bool IsOpen { get; private set; }
    }
}
