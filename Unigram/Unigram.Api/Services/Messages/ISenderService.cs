using Telegram.Api.TL;

namespace Telegram.Api.Services.Messages
{
    public interface ISenderService
    {
        void Send(TLMessageBase message);
        void ResendAll();

        void Open();
        void Close();
    }
}
