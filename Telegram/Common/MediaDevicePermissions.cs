using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Telegram.Controls;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.System;
using Windows.System.Profile;

namespace Telegram.Common
{
    public enum MediaDeviceAccess
    {
        Audio,
        Video,
        AudioAndVideo
    }

    public partial class MediaDevicePermissions
    {
        public static bool IsUnsupported(XamlRoot xamlRoot)
        {
            if (ApiInfo.IsMediaSupported)
            {
                return false;
            }

            // VoIP isn't supported on Windows N because:
            // - MediaCapture is used for capturing video (no alternatives on WinRT)
            // - MediaFoundation is used for encoding/decoding video frames (can fallback for WebRTC's software)
            _ = MessagePopup.ShowAsync(xamlRoot, Strings.VoipPlatformUnsupportedText, Strings.VoipPlatformUnsupportedTitle, Strings.OK);
            return true;
        }

        public static Task<bool> CheckAccessAsync(XamlRoot xamlRoot, MediaDeviceAccess requestedAccess, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var captureMode = requestedAccess switch
            {
                MediaDeviceAccess.Audio => StreamingCaptureMode.Audio,
                MediaDeviceAccess.Video => StreamingCaptureMode.Video,
                _ => StreamingCaptureMode.AudioAndVideo
            };

            if (captureMode == StreamingCaptureMode.Audio)
            {
                var access = GetAccess(DeviceClass.AudioCapture);
                return CheckDeviceAccessAsync(xamlRoot, captureMode, access, true, requestedTheme);
            }
            else if (captureMode == StreamingCaptureMode.Video)
            {
                var access = GetAccess(DeviceClass.VideoCapture);
                return CheckDeviceAccessAsync(xamlRoot, captureMode, access, true, requestedTheme);
            }

            var audio = GetAccess(DeviceClass.AudioCapture);
            var video = GetAccess(DeviceClass.VideoCapture);

            if (audio == DeviceAccessStatus.Unspecified && video == DeviceAccessStatus.Unspecified)
            {
                return CheckDeviceAccessAsync(xamlRoot, captureMode, audio, true, requestedTheme);
            }
            else if (audio == DeviceAccessStatus.Unspecified && video == DeviceAccessStatus.Allowed)
            {
                return CheckDeviceAccessAsync(xamlRoot, StreamingCaptureMode.Audio, audio, true, requestedTheme);
            }
            else if (audio == DeviceAccessStatus.Allowed && video == DeviceAccessStatus.Unspecified)
            {
                return CheckDeviceAccessAsync(xamlRoot, StreamingCaptureMode.Video, video, true, requestedTheme);
            }
            else if (audio != DeviceAccessStatus.Allowed && video != DeviceAccessStatus.Allowed)
            {
                return CheckDeviceAccessAsync(xamlRoot, captureMode, audio, true, requestedTheme);
            }
            else if (audio != DeviceAccessStatus.Allowed)
            {
                return CheckDeviceAccessAsync(xamlRoot, StreamingCaptureMode.Audio, audio, true, requestedTheme);
            }
            else if (video != DeviceAccessStatus.Allowed)
            {
                return CheckDeviceAccessAsync(xamlRoot, StreamingCaptureMode.Video, video, true, requestedTheme);
            }

            return Task.FromResult(true);
        }

        private static async Task<bool> CheckDeviceAccessAsync(XamlRoot xamlRoot, StreamingCaptureMode captureMode, DeviceAccessStatus access, bool required, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (access == DeviceAccessStatus.Unspecified)
            {
                MediaCapture capture = null;
                bool success = false;
                try
                {
                    capture = new MediaCapture();
                    var settings = new MediaCaptureInitializationSettings();
                    settings.StreamingCaptureMode = captureMode;
                    await capture.InitializeAsync(settings);
                    success = true;
                }
                catch
                {
                    success = !required;
                }
                finally
                {
                    capture?.Dispose();
                }

                return success;
            }
            else if (access != DeviceAccessStatus.Allowed && required)
            {
                var popup = new MessagePopup
                {
                    Title = Strings.AppName,
                    Message = captureMode switch
                    {
                        StreamingCaptureMode.Audio => Strings.PermissionNoAudioCalls,
                        StreamingCaptureMode.Video => Strings.PermissionNoVideoCalls,
                        _ => Strings.PermissionNoAudioVideoCalls
                    },
                    PrimaryButtonText = Strings.Settings,
                    SecondaryButtonText = Strings.OK,
                    RequestedTheme = requestedTheme
                };

                var confirm = await popup.ShowQueuedAsync(xamlRoot);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                }

                return false;
            }

            return true;
        }

        private static DeviceAccessStatus GetAccess(DeviceClass deviceClass)
        {
            // For some reason, as far as I understood, CurrentStatus is always Unspecified on Xbox
            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Xbox"))
            {
                return DeviceAccessStatus.Allowed;
            }

            try
            {
                var access = DeviceAccessInformation.CreateFromDeviceClass(deviceClass);
                return access.CurrentStatus;
            }
            catch
            {
                return DeviceAccessStatus.Unspecified;
            }
        }
    }
}
