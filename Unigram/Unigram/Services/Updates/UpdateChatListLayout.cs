using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Services.Updates
{
    public class UpdateChatListLayout
    {
        public UpdateChatListLayout(bool threeLines)
        {
            UseThreeLinesLayout = threeLines;
        }

        public bool UseThreeLinesLayout { get; private set; }
    }
}
