using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

        public static async Task<bool> CheckAccessAsync(XamlRoot xamlRoot, MediaDeviceAccess requestedAccess, ElementTheme requestedTheme = ElementTheme.Default)
        {
            string[] capabilities;

            if (requestedAccess == MediaDeviceAccess.AudioAndVideo)
            {
                capabilities = new[] { "microphone", "webcam" };
            }
            else if (requestedAccess == MediaDeviceAccess.Audio)
            {
                capabilities = new[] { "microphone" };
            }
            else
            {
                capabilities = new[] { "webcam" };
            }

            IReadOnlyDictionary<string, AppCapabilityAccessStatus> access;
            try
            {
                access = await AppCapability.RequestAccessForCapabilitiesAsync(capabilities);
            }
            catch
            {
                access = capabilities.ToDictionary(x => x, y => AppCapabilityAccessStatus.DeniedBySystem);
            }

            access.TryGetValue("microphone", out AppCapabilityAccessStatus audio);
            access.TryGetValue("webcam", out AppCapabilityAccessStatus video);

            if (requestedAccess == MediaDeviceAccess.AudioAndVideo)
            {
                if (audio != AppCapabilityAccessStatus.Allowed && video != AppCapabilityAccessStatus.Allowed)
                {
                    ShowPopup(xamlRoot, MediaDeviceAccess.AudioAndVideo, requestedTheme);
                    return false;
                }
                else if (audio != AppCapabilityAccessStatus.Allowed)
                {
                    ShowPopup(xamlRoot, MediaDeviceAccess.Audio, requestedTheme);
                    return false;
                }
                else if (video != AppCapabilityAccessStatus.Allowed)
                {
                    ShowPopup(xamlRoot, MediaDeviceAccess.Video, requestedTheme);
                    return false;
                }
            }
            else if (requestedAccess == MediaDeviceAccess.Audio && audio != AppCapabilityAccessStatus.Allowed)
            {
                ShowPopup(xamlRoot, MediaDeviceAccess.Audio, requestedTheme);
                return false;
            }
            else if (requestedAccess == MediaDeviceAccess.Video && video != AppCapabilityAccessStatus.Allowed)
            {
                ShowPopup(xamlRoot, MediaDeviceAccess.Video, requestedTheme);
                return false;
            }

            return true;
        }

        private static async void ShowPopup(XamlRoot xamlRoot, MediaDeviceAccess requestedAccess, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = new MessagePopup
            {
                Title = Strings.AppName,
                Message = requestedAccess switch
                {
                    MediaDeviceAccess.Audio => Strings.PermissionNoAudioCalls,
                    MediaDeviceAccess.Video => Strings.PermissionNoVideoCalls,
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
        }
    }
}
