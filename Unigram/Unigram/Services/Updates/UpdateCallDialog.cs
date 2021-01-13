using Telegram.Td.Api;

namespace Unigram.Services.Updates
{
    public class UpdateCallDialog
    {
        public UpdateCallDialog(Call call)
        {
            Call = call;
        }

        public UpdateCallDialog(GroupCall call)
        {
            GroupCall = call;
        }

        public UpdateCallDialog()
        {

        }

        public Call Call { get; private set; }
        public GroupCall GroupCall { get; private set; }
    }
}
