//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Calls;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.System.Display;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    public static partial class Extensions
    {
        public static void TryReportCompleted(this ShareOperation operation)
        {
            try
            {
                operation.ReportCompleted();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryReportDataRetrieved(this ShareOperation operation)
        {
            try
            {
                operation.ReportDataRetrieved();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryReportError(this ShareOperation operation, string value)
        {
            try
            {
                operation.ReportError(value);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryProcessDownEvent(this GestureRecognizer recognizer, PointerPoint value)
        {
            try
            {
                recognizer.ProcessDownEvent(value);
            }
            catch
            {
                recognizer.TryCompleteGesture();
            }
        }

        public static void TryProcessMoveEvents(this GestureRecognizer recognizer, IList<PointerPoint> value)
        {
            try
            {
                recognizer.ProcessMoveEvents(value);
            }
            catch
            {
                recognizer.TryCompleteGesture();
            }
        }

        public static void TryProcessUpEvent(this GestureRecognizer recognizer, PointerPoint value)
        {
            try
            {
                recognizer.ProcessUpEvent(value);
            }
            catch
            {
                recognizer.TryCompleteGesture();
            }
        }

        public static void TryCompleteGesture(this GestureRecognizer recognizer)
        {
            try
            {
                if (recognizer.IsActive)
                {
                    recognizer.CompleteGesture();
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static async Task<VoipPhoneCallResourceReservationStatus> TryReserveCallResourcesAsync(this VoipCallCoordinator coordinator)
        {
            var status = VoipPhoneCallResourceReservationStatus.ResourcesNotAvailable;
            try
            {
                status = await coordinator.ReserveCallResourcesAsync();
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147024713)
                {
                    // CPU and memory resources have already been reserved for the app.
                    // Ignore the return value from your call to ReserveCallResourcesAsync,
                    // and proceed to handle a new VoIP call.
                    status = VoipPhoneCallResourceReservationStatus.Success;
                }
            }

            return status;
        }

        public static void TryNotifyMutedChanged(this VoipCallCoordinator coordinator, bool muted)
        {
            try
            {
                if (muted)
                {
                    coordinator?.NotifyMuted();
                }
                else
                {
                    coordinator?.NotifyUnmuted();
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryNotifyCallActive(this VoipPhoneCall call)
        {
            try
            {
                call.NotifyCallActive();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryNotifyCallEnded(this VoipPhoneCall call)
        {
            try
            {
                call.NotifyCallEnded();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryRequestActive(this DisplayRequest displayRequest)
        {
            try
            {
                displayRequest.RequestActive();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryRequestRelease(this DisplayRequest displayRequest)
        {
            try
            {
                displayRequest.RequestRelease();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static bool TryChangeView(this ScrollViewer scrollViewer, double? horizontalOffset, double? verticalOffset, float? zoomFactor)
        {
            try
            {
                return scrollViewer.ChangeView(horizontalOffset, verticalOffset, zoomFactor);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
                return false;
            }
        }

        public static bool TryChangeView(this ScrollViewer scrollViewer, double? horizontalOffset, double? verticalOffset, float? zoomFactor, bool disableAnimation)
        {
            try
            {
                return scrollViewer.ChangeView(horizontalOffset, verticalOffset, zoomFactor, disableAnimation);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
                return false;
            }
        }

        public static bool TryGetContent<T>(this XamlRoot xamlRoot, out T content)
        {
            try
            {
                if (xamlRoot.Content is T cast)
                {
                    content = cast;
                    return true;
                }
            }
            catch
            {
                // XamlRoot.Content seems to throw a NullReferenceException
                // whenever corresponding window has been already closed.
            }

            content = default;
            return false;
        }

        /// <summary>
        /// The subscriber, control or other projected object this came out of is gone, and calling
        /// it again will only throw again.
        ///
        /// One signal on .NET Native - the RCW was separated from its object - and three on
        /// CsWinRT, which is why this is asked here rather than in a catch filter that only ever
        /// covers whichever runtime the author had in mind.
        ///
        /// Two neighbours are deliberately not here. RPC_E_WRONG_THREAD says the object is alive
        /// and was called from the wrong thread, and E_NOINTERFACE out of a QueryInterface is as
        /// likely to be a missing CsWinRT manifest entry as a dead peer - both are bugs to fix,
        /// and treating either as "gone" would drop the subscriber that would have reported it.
        /// </summary>
        public static bool IsInvalidComObject(this Exception ex)
        {
            const int RPC_E_DISCONNECTED = unchecked((int)0x80010108);
            const int CO_E_OBJNOTCONNECTED = unchecked((int)0x800401FD);
            const int RO_E_CLOSED = unchecked((int)0x80000013);

            return ex is InvalidComObjectException
                || ex is ObjectDisposedException
                || (ex is COMException com && com.HResult is RPC_E_DISCONNECTED or CO_E_OBJNOTCONNECTED or RO_E_CLOSED);
        }
    }
}
