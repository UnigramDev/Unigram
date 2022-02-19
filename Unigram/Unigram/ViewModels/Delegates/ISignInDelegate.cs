namespace Unigram.ViewModels.Delegates
{
    public interface ISignInDelegate : IViewModelDelegate
    {
        void UpdateQrCodeMode(QrCodeMode mode);
        void UpdateQrCode(string link, bool firstTime);
    }

    public enum QrCodeMode
    {
        Loading,
        Primary,
        Secondary,
        Disabled
    }
}
