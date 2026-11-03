//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if UNPACKAGED
using System;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Telegram.Common
{
    /// <summary>
    /// The manifest's StartupTask, for a process with no manifest: a value under the user's Run key
    /// that starts the app with <see cref="Argument"/>. It answers in <see cref="StartupTaskState"/>
    /// so that the settings toggle reads both the same way.
    /// </summary>
    public static class UnpackagedStartup
    {
        public const string Argument = "-autostart";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Where Startup apps in Task Manager and Settings record the user's choice, under the same
        // value name. Binary, and the entry is turned off while the first byte is odd.
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        // The application id, so a debug build has an entry of its own rather than repointing the
        // installed app's at the debug executable. The installer deletes it by this name.
        private const string ValueName = Toast.UnpackagedApplicationId;

        public static bool IsStartedBy(string[] args)
        {
            return Array.Exists(args, x => string.Equals(x, Argument, StringComparison.OrdinalIgnoreCase));
        }

        public static StartupTaskState GetState()
        {
            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKey);
                if (run?.GetValue(ValueName) == null)
                {
                    return StartupTaskState.Disabled;
                }

                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
                if (approved?.GetValue(ValueName) is byte[] { Length: > 0 } choice && (choice[0] & 1) != 0)
                {
                    return StartupTaskState.DisabledByUser;
                }

                return StartupTaskState.Enabled;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return StartupTaskState.Disabled;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using var run = Registry.CurrentUser.CreateSubKey(RunKey);

                if (enabled)
                {
                    run.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {Argument}");
                }
                else
                {
                    run.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
    }
}
#endif
