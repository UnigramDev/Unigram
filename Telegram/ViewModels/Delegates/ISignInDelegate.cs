//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.ViewModels.Delegates
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
