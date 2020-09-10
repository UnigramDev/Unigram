using Telegram.Td.Api;

namespace Unigram.Services.Updates
{
    public class UpdateCallDialog
    {
        public UpdateCallDialog(Call call, bool open)
        {
            Call = call;
            IsOpen = open;
        }

        public Call Call { get; private set; }
        public bool IsOpen { get; private set; }
    }
}
