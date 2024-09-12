using LibVLCSharp.Shared.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// Dialogs can be raised by libvlc for network actions and logins.
    /// You may only call once PostLogin or PostAction or Dismiss after which this instance will be invalid.
    /// </summary>
    public partial class Dialog
    {
        IntPtr _id;

        struct Native
        {
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_dialog_post_login")]
            internal static extern int LibVLCDialogPostLogin(IntPtr dialogId, IntPtr username, IntPtr password, bool store);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_dialog_post_action")]
            internal static extern int LibVLCDialogPostAction(IntPtr dialogId, int actionIndex);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_dialog_dismiss")]
            internal static extern int LibVLCDialogDismiss(IntPtr dialogId);
        }

        Dialog(IntPtr id)
        {
            if (id == IntPtr.Zero)
                throw new ArgumentNullException(nameof(id));
            _id = id;
        }

        internal Dialog(DialogId id) : this(id.NativeReference)
        {
        }

        /// <summary>
        /// Post a login answer.
        /// After this call, the instance won't be valid anymore
        /// </summary>
        /// <param name="username">valid non-empty string</param>
        /// <param name="password">valid string</param>
        /// <param name="store">if true stores the credentials</param>
        /// <returns></returns>
        public bool PostLogin(string username, string password, bool store)
        {
            if (_id == IntPtr.Zero)
                throw new VLCException("Calling method on dismissed Dialog instance");

            username ??= string.Empty;
            password ??= string.Empty;

            var usernamePtr = username.ToUtf8();
            var passwordPtr = password.ToUtf8();

            var result = MarshalUtils.PerformInteropAndFree(
                () => Native.LibVLCDialogPostLogin(_id, usernamePtr, passwordPtr, store),
                usernamePtr, passwordPtr) == 0;

            _id = IntPtr.Zero;

            return result;
        }

        /// <summary>
        /// Post a question answer.
        /// After this call, this instance won't be valid anymore
        /// QuestionCb
        /// </summary>
        /// <param name="actionIndex">1 for action1, 2 for action2</param>
        /// <returns>return true on success, false otherwise</returns>
        public bool PostAction(int actionIndex)
        {
            if (_id == IntPtr.Zero)
                throw new VLCException("Calling method on dismissed Dialog instance");

            var result = Native.LibVLCDialogPostAction(_id, actionIndex) == 0;
            _id = IntPtr.Zero;

            return result;
        }

        /// <summary>
        /// Dismiss a dialog.
        /// After this call, this instance won't be valid anymore
        /// </summary>
        /// <returns>true if properly dismissed, false otherwise</returns>
        public bool Dismiss()
        {
            if (_id == IntPtr.Zero) return false;

            var result = Native.LibVLCDialogDismiss(_id) == 0;
            _id = IntPtr.Zero;

            return result;
        }
    }

    internal readonly struct DialogId
    {
        internal DialogId(IntPtr nativeReference)
        {
            NativeReference = nativeReference;
        }
        internal IntPtr NativeReference { get; }
    }

    /// <summary>
    /// Describes the question type by libvlc
    /// </summary>
    public enum DialogQuestionType
    {
        /// <summary>
        /// Normal question
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Warning question
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Critical question
        /// </summary>
        Critical = 2
    }

    /// <summary>
    /// Called when an error message needs to be displayed.
    /// </summary>
    /// <param name="title">title of the dialog </param>
    /// <param name="text">text of the dialog </param>
    public delegate Task DisplayError(string title, string text);

    /// <summary>
    /// Called when a login dialog needs to be displayed.
    /// You can interact with this dialog by calling PostLogin() to post an answer or Dismiss() to cancel this dialog.
    /// </summary>
    /// <param name="dialog">id used to interact with the dialog </param>
    /// <param name="title">title of the dialog </param>
    /// <param name="text">text of the dialog </param>
    /// <param name="defaultUsername">user name that should be set on the user form</param>
    /// <param name="askStore">if true, ask the user if he wants to save the credentials</param>
    /// <param name="token">Use token to cancel operation</param>
    public delegate Task DisplayLogin(Dialog dialog, string title, string text, string defaultUsername, bool askStore, CancellationToken token);

    /// <summary>
    /// Called when a question dialog needs to be displayed.
    /// You can interact with this dialog by calling PostAction() to post an answer or Dismiss() to cancel this dialog.
    /// </summary>
    /// <param name="dialog">id used to interact with the dialog</param>
    /// <param name="title">title of the dialog</param>
    /// <param name="text">text of the dialog</param>
    /// <param name="type">question type (or severity) of the dialog</param>
    /// <param name="cancelText">text of the cancel button</param>
    /// <param name="firstActionText">text of the first button, if NULL, don't display this button</param>
    /// <param name="secondActionText">text of the second button, if NULL, don't display this button</param>
    /// <param name="token">Use token to cancel operation</param>
    public delegate Task DisplayQuestion(Dialog dialog, string title, string text, DialogQuestionType type, string cancelText,
        string firstActionText, string secondActionText, CancellationToken token);

    /// <summary>
    /// Called when a progress dialog needs to be displayed.
    /// If cancellable cancelText is not NULL, you can cancel this dialog by calling libvlc_dialog_dismiss()
    /// </summary>
    /// <param name="dialog">id used to interact with the dialog</param>
    /// <param name="title">title of the dialog</param>
    /// <param name="text">text of the dialog</param>
    /// <param name="indeterminate">true if the progress dialog is indeterminate</param>
    /// <param name="position">initial position of the progress bar (between 0.0 and 1.0)</param>
    /// <param name="cancelText">text of the cancel button, if NULL the dialog is not cancellable</param>
    /// <param name="token">Use token to cancel operation</param>
    public delegate Task DisplayProgress(Dialog dialog, string title, string text, bool indeterminate, float position, string cancelText, CancellationToken token);

    /// <summary>
    /// Called when a progress dialog needs to be updated.
    /// </summary>
    /// <param name="dialog">id of the dialog</param>
    /// <param name="position">position of the progress bar (between 0.0 and 1.0)</param>
    /// <param name="text">new text of the progress dialog </param>
    public delegate Task UpdateProgress(Dialog dialog, float position, string text);
}
