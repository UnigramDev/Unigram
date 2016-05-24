namespace Telegram.Api.TL.Interfaces
{
    public interface IInputPeer
    {
        TLInputPeerBase ToInputPeer();

        string GetUnsendedTextFileName();
    }
}
