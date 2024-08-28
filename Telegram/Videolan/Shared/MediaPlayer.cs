using LibVLCSharp.Shared.Helpers;
using LibVLCSharp.Shared.Structures;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// The MediaPlayer type is used to control playback, set renderers, provide events and much more
    /// </summary>
    public partial class MediaPlayer : Internal
    {
        readonly struct Native
        {

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_new")]
            internal static extern IntPtr LibVLCMediaPlayerNew(LibVLC libvlc);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_release")]
            internal static extern void LibVLCMediaPlayerRelease(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_new_from_media")]
            internal static extern IntPtr LibVLCMediaPlayerNewFromMedia(Media media);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_media")]
            internal static extern void LibVLCMediaPlayerSetMedia(IntPtr mediaPlayer, Media media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_media")]
            internal static extern void LibVLCMediaPlayerSetMediaPtr(IntPtr mediaPlayer, IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_media")]
            internal static extern IntPtr LibVLCMediaPlayerGetMedia(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_event_manager")]
            internal static extern IntPtr LibVLCMediaPlayerEventManager(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_is_playing")]
            internal static extern int LibVLCMediaPlayerIsPlaying(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_play")]
            internal static extern int LibVLCMediaPlayerPlay(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_pause")]
            internal static extern void LibVLCMediaPlayerSetPause(IntPtr mediaPlayer, bool pause);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_pause")]
            internal static extern void LibVLCMediaPlayerPause(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_stop")]
            internal static extern void LibVLCMediaPlayerStop(IntPtr mediaPlayer);

#if APPLE || DESKTOP
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_nsobject")]
            internal static extern void LibVLCMediaPlayerSetNsobject(IntPtr mediaPlayer, IntPtr drawable);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_nsobject")]
            internal static extern IntPtr LibVLCMediaPlayerGetNsobject(IntPtr mediaPlayer);
#endif
#if DESKTOP
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_xwindow")]
            internal static extern void LibVLCMediaPlayerSetXwindow(IntPtr mediaPlayer, uint drawable);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_xwindow")]
            internal static extern uint LibVLCMediaPlayerGetXwindow(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_hwnd")]
            internal static extern void LibVLCMediaPlayerSetHwnd(IntPtr mediaPlayer, IntPtr drawable);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_hwnd")]
            internal static extern IntPtr LibVLCMediaPlayerGetHwnd(IntPtr mediaPlayer);
#endif
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_length")]
            internal static extern long LibVLCMediaPlayerGetLength(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_time")]
            internal static extern long LibVLCMediaPlayerGetTime(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_time")]
            internal static extern void LibVLCMediaPlayerSetTime(IntPtr mediaPlayer, long time);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_position")]
            internal static extern float LibVLCMediaPlayerGetPosition(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_position")]
            internal static extern void LibVLCMediaPlayerSetPosition(IntPtr mediaPlayer, float position);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_chapter")]
            internal static extern void LibVLCMediaPlayerSetChapter(IntPtr mediaPlayer, int chapter);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_chapter")]
            internal static extern int LibVLCMediaPlayerGetChapter(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_chapter_count")]
            internal static extern int LibVLCMediaPlayerGetChapterCount(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_will_play")]
            internal static extern int LibVLCMediaPlayerWillPlay(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_chapter_count_for_title")]
            internal static extern int LibVLCMediaPlayerGetChapterCountForTitle(IntPtr mediaPlayer, int title);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_title")]
            internal static extern void LibVLCMediaPlayerSetTitle(IntPtr mediaPlayer, int title);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_title")]
            internal static extern int LibVLCMediaPlayerGetTitle(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_title_count")]
            internal static extern int LibVLCMediaPlayerGetTitleCount(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_previous_chapter")]
            internal static extern void LibVLCMediaPlayerPreviousChapter(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_next_chapter")]
            internal static extern void LibVLCMediaPlayerNextChapter(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_rate")]
            internal static extern float LibVLCMediaPlayerGetRate(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_rate")]
            internal static extern int LibVLCMediaPlayerSetRate(IntPtr mediaPlayer, float rate);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_state")]
            internal static extern VLCState LibVLCMediaPlayerGetState(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_fps")]
            internal static extern float LibVLCMediaPlayerGetFps(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_has_vout")]
            internal static extern uint LibVLCMediaPlayerHasVout(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_is_seekable")]
            internal static extern int LibVLCMediaPlayerIsSeekable(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_can_pause")]
            internal static extern int LibVLCMediaPlayerCanPause(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_program_scrambled")]
            internal static extern int LibVLCMediaPlayerProgramScrambled(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_next_frame")]
            internal static extern void LibVLCMediaPlayerNextFrame(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_navigate")]
            internal static extern void LibVLCMediaPlayerNavigate(IntPtr mediaPlayer, uint navigate);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_video_title_display")]
            internal static extern void LibVLCMediaPlayerSetVideoTitleDisplay(IntPtr mediaPlayer, Position position, uint timeout);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_toggle_fullscreen")]
            internal static extern void LibVLCToggleFullscreen(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_set_fullscreen")]
            internal static extern void LibVLCSetFullscreen(IntPtr mediaPlayer, int fullscreen);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_get_fullscreen")]
            internal static extern int LibVLCGetFullscreen(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_toggle_teletext")]
            internal static extern void LibVLCToggleTeletext(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_equalizer")]
            internal static extern int LibVLCMediaPlayerSetEqualizer(IntPtr mediaPlayer, Equalizer equalizer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_equalizer")]
            internal static extern int LibVLCMediaPlayerSetEqualizerPtr(IntPtr mediaPlayer, IntPtr equalizer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_callbacks")]
            internal static extern void LibVLCAudioSetCallbacks(IntPtr mediaPlayer, LibVLCAudioPlayCb play, LibVLCAudioPauseCb pause,
                LibVLCAudioResumeCb resume, LibVLCAudioFlushCb flush, LibVLCAudioDrainCb drain, IntPtr opaque);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_volume_callback")]
            internal static extern void LibVLCAudioSetVolumeCallback(IntPtr mediaPlayer, LibVLCVolumeCb volumeCallback);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_format_callbacks")]
            internal static extern void LibVLCAudioSetFormatCallbacks(IntPtr mediaPlayer, LibVLCAudioSetupCb setup, LibVLCAudioCleanupCb cleanup);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_format")]
            internal static extern void LibVLCAudioSetFormat(IntPtr mediaPlayer, IntPtr format,
                uint rate, uint channels);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_output_device_enum")]
            internal static extern IntPtr LibVLCAudioOutputDeviceEnum(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_output_device_set")]
            internal static extern void LibVLCAudioOutputDeviceSet(IntPtr mediaPlayer, IntPtr module, IntPtr deviceId);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_output_device_get")]
            internal static extern IntPtr LibVLCAudioOutputDeviceGet(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_output_set")]
            internal static extern int LibVLCAudioOutputSet(IntPtr mediaPlayer, IntPtr name);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_toggle_mute")]
            internal static extern void LibVLCAudioToggleMute(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_get_mute")]
            internal static extern int LibVLCAudioGetMute(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_mute")]
            internal static extern void LibVLCAudioSetMute(IntPtr mediaPlayer, int status);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_get_volume")]
            internal static extern int LibVLCAudioGetVolume(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_volume")]
            internal static extern int LibVLCAudioSetVolume(IntPtr mediaPlayer, int volume);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_get_track_count")]
            internal static extern int LibVLCAudioGetTrackCount(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_get_track_description")]
            internal static extern IntPtr LibVLCAudioGetTrackDescription(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_get_track")]
            internal static extern int LibVLCAudioGetTrack(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_track")]
            internal static extern int LibVLCAudioSetTrack(IntPtr mediaPlayer, int track);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_get_channel")]
            internal static extern AudioOutputChannel LibVLCAudioGetChannel(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_channel")]
            internal static extern int LibVLCAudioSetChannel(IntPtr mediaPlayer, AudioOutputChannel channel);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_get_delay")]
            internal static extern long LibVLCAudioGetDelay(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_set_delay")]
            internal static extern int LibVLCAudioSetDelay(IntPtr mediaPlayer, long delay);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_callbacks")]
            internal static extern void LibVLCVideoSetCallbacks(IntPtr mediaPlayer, LibVLCVideoLockCb lockCallback,
                LibVLCVideoUnlockCb unlock, LibVLCVideoDisplayCb display, IntPtr opaque);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_format")]
            internal static extern void LibVLCVideoSetFormat(IntPtr mediaPlayer, IntPtr chroma,
                uint width, uint height, uint pitch);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_format_callbacks")]
            internal static extern void LibVLCVideoSetFormatCallbacks(IntPtr mediaPlayer, LibVLCVideoFormatCb setup,
                LibVLCVideoCleanupCb cleanup);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_key_input")]
            internal static extern void LibVLCVideoSetKeyInput(IntPtr mediaPlayer, int enable);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_mouse_input")]
            internal static extern void LibVLCVideoSetMouseInput(IntPtr mediaPlayer, int enable);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_size")]
            internal static extern int LibVLCVideoGetSize(IntPtr mediaPlayer, uint num, ref uint px, ref uint py);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_cursor")]
            internal static extern int LibVLCVideoGetCursor(IntPtr mediaPlayer, uint num, ref int px, ref int py);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_scale")]
            internal static extern float LibVLCVideoGetScale(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_scale")]
            internal static extern void LibVLCVideoSetScale(IntPtr mediaPlayer, float factor);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_aspect_ratio")]
            internal static extern IntPtr LibVLCVideoGetAspectRatio(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_aspect_ratio")]
            internal static extern void LibVLCVideoSetAspectRatio(IntPtr mediaPlayer, IntPtr aspect);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_spu")]
            internal static extern int LibVLCVideoGetSpu(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_spu_count")]
            internal static extern int LibVLCVideoGetSpuCount(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_spu_description")]
            internal static extern IntPtr LibVLCVideoGetSpuDescription(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_spu")]
            internal static extern int LibVLCVideoSetSpu(IntPtr mediaPlayer, int spu);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_spu_delay")]
            internal static extern long LibVLCVideoGetSpuDelay(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_spu_delay")]
            internal static extern int LibVLCVideoSetSpuDelay(IntPtr mediaPlayer, long delay);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_title_description")]
            internal static extern IntPtr LibVLCVideoGetTitleDescription(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_full_title_descriptions")]
            internal static extern int LibVLCMediaPlayerGetFullTitleDescriptions(IntPtr mediaPlayer, IntPtr titles);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_chapter_description")]
            internal static extern IntPtr LibVLCVideoGetChapterDescription(IntPtr mediaPlayer,
                int title);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_title_descriptions_release")]
            internal static extern void LibVLCTitleDescriptionsRelease(IntPtr titles, uint count);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_full_chapter_descriptions")]
            internal static extern int LibVLCMediaPlayerGetFullChapterDescriptions(IntPtr mediaPlayer, int titleIndex, out IntPtr chapters);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_chapter_descriptions_release")]
            internal static extern void LibVLCChapterDescriptionsRelease(IntPtr chapters, uint count);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_crop_geometry")]
            internal static extern IntPtr LibVLCVideoGetCropGeometry(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_crop_geometry")]
            internal static extern void LibVLCVideoSetCropGeometry(IntPtr mediaPlayer, IntPtr geometry);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_teletext")]
            internal static extern int LibVLCVideoGetTeletext(IntPtr mediaPlayer);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_teletext")]
            internal static extern void LibVLCVideoSetTeletext(IntPtr mediaPlayer, int page);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_track_count")]
            internal static extern int LibVLCVideoGetTrackCount(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_track_description")]
            internal static extern IntPtr LibVLCVideoGetTrackDescription(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_track")]
            internal static extern int LibVLCVideoGetTrack(IntPtr mediaPlayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_track")]
            internal static extern int LibVLCVideoSetTrack(IntPtr mediaPlayer, int track);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_take_snapshot")]
            internal static extern int LibVLCVideoTakeSnapshot(IntPtr mediaPlayer, uint num,
                IntPtr filepath, uint width, uint height);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_deinterlace")]
            internal static extern void LibVLCVideoSetDeinterlace(IntPtr mediaPlayer, IntPtr mode);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_marquee_int")]
            internal static extern int LibVLCVideoGetMarqueeInt(IntPtr mediaPlayer, VideoMarqueeOption option);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_marquee_string")]
            internal static extern IntPtr LibVLCVideoGetMarqueeString(IntPtr mediaPlayer, VideoMarqueeOption option);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_marquee_int")]
            internal static extern void LibVLCVideoSetMarqueeInt(IntPtr mediaPlayer, VideoMarqueeOption option, int marqueeValue);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_marquee_string")]
            internal static extern void LibVLCVideoSetMarqueeString(IntPtr mediaPlayer, VideoMarqueeOption option, IntPtr marqueeValue);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_logo_int")]
            internal static extern int LibVLCVideoGetLogoInt(IntPtr mediaPlayer, VideoLogoOption option);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_logo_int")]
            internal static extern void LibVLCVideoSetLogoInt(IntPtr mediaPlayer, VideoLogoOption option, int value);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_logo_string")]
            internal static extern void LibVLCVideoSetLogoString(IntPtr mediaPlayer, VideoLogoOption option, IntPtr logoOptionValue);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_adjust_int")]
            internal static extern int LibVLCVideoGetAdjustInt(IntPtr mediaPlayer, VideoAdjustOption option);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_adjust_int")]
            internal static extern void LibVLCVideoSetAdjustInt(IntPtr mediaPlayer, VideoAdjustOption option, int value);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_get_adjust_float")]
            internal static extern float LibVLCVideoGetAdjustFloat(IntPtr mediaPlayer, VideoAdjustOption option);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_set_adjust_float")]
            internal static extern void LibVLCVideoSetAdjustFloat(IntPtr mediaPlayer, VideoAdjustOption option, float value);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_add_slave")]
            internal static extern int LibVLCMediaPlayerAddSlave(IntPtr mediaPlayer, MediaSlaveType mediaSlaveType,
                IntPtr uri, bool selectWhenloaded);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_new_viewpoint")]
            internal static extern IntPtr LibVLCVideoNewViewpoint();

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_video_update_viewpoint")]
            internal static extern int LibVLCVideoUpdateViewpoint(IntPtr mediaPlayer, IntPtr viewpoint, bool absolute);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_track_description_list_release")]
            internal static extern void LibVLCTrackDescriptionListRelease(IntPtr trackDescription);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_output_device_list_release")]
            internal static extern void LibVLCAudioOutputDeviceListRelease(IntPtr list);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_renderer")]
            internal static extern int LibVLCMediaPlayerSetRenderer(IntPtr mediaplayer, RendererItem renderItem);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_renderer")]
            internal static extern int LibVLCMediaPlayerSetRendererPtr(IntPtr mediaplayer, IntPtr renderItem);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_get_role")]
            internal static extern MediaPlayerRole LibVLCMediaPlayerGetRole(IntPtr mediaplayer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_role")]
            internal static extern int LibVLCMediaPlayerSetRole(IntPtr mediaplayer, MediaPlayerRole role);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_retain")]
            internal static extern void LibVLCMediaPlayerRetain(IntPtr mediaplayer);
#if ANDROID

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_player_set_android_context")]
            internal static extern void LibVLCMediaPlayerSetAndroidContext(IntPtr mediaPlayer, IntPtr aWindow);
#endif
        }

        MediaPlayerEventManager _eventManager;

        /// <summary>
        /// The GCHandle to be passed to callbacks as userData
        /// </summary>
        GCHandle _gcHandle;

        /// <summary>Create an empty Media Player object</summary>
        /// <param name="libVLC">
        /// <para>the libvlc instance in which the Media Player</para>
        /// <para>should be created.</para>
        /// </param>
        /// <returns>a new media player object, or NULL on error.</returns>
        public MediaPlayer(LibVLC libVLC)
            : base(Native.LibVLCMediaPlayerNew(libVLC))
        {
        }

        /// <summary>Create a Media Player object from a Media</summary>
        /// <param name="media">
        /// <para>the media. Afterwards the p_md can be safely</para>
        /// <para>destroyed.</para>
        /// </param>
        /// <returns>a new media player object, or NULL on error.</returns>
        public MediaPlayer(Media media)
            : base(Native.LibVLCMediaPlayerNewFromMedia(media))
        {
        }

        protected override bool ReleaseHandle()
        {
            Native.LibVLCMediaPlayerRelease(handle);
            return true;
        }

        /// <summary>
        /// Get the media used by the media_player.
        /// Set the media that will be used by the media_player.
        /// If any, previous md will be released.
        /// Note: It is safe to release the Media on the C# side after it's been set on the MediaPlayer successfully
        /// </summary>
        public Media Media
        {
            get
            {
                var mediaPtr = Native.LibVLCMediaPlayerGetMedia(handle);
                return mediaPtr == IntPtr.Zero ? null : new Media(mediaPtr);
            }
            set
            {
                if (value != null)
                    Native.LibVLCMediaPlayerSetMedia(handle, value);
                else
                    Native.LibVLCMediaPlayerSetMediaPtr(handle, IntPtr.Zero);
            }
        }

        /// <summary>
        /// return true if the media player is playing, false otherwise
        /// </summary>
        public bool IsPlaying => Native.LibVLCMediaPlayerIsPlaying(handle) != 0;

        /// <summary>
        /// Start playback with Media that is set
        /// If playback was already started, this method has no effect
        /// </summary>
        /// <returns>true if successful</returns>
        public bool Play()
        {
            var media = Media;
            if (media != null)
            {
                media.AddOption(Configuration);
                media.Dispose();
            }
            return Native.LibVLCMediaPlayerPlay(handle) == 0;
        }

        /// <summary>
        /// Set media and start playback
        /// </summary>
        /// <param name="media"></param>
        /// <returns>true if successful</returns>
        public bool Play(Media media)
        {
            Media = media;
            return Play();
        }

        /// <summary>
        /// Pause or resume (no effect if there is no media).
        /// version LibVLC 1.1.1 or later
        /// </summary>
        /// <param name="pause">play/resume if true, pause if false</param>
        public void SetPause(bool pause) => Native.LibVLCMediaPlayerSetPause(handle, pause);

        /// <summary>
        /// Toggle pause (no effect if there is no media)
        /// </summary>
        public void Pause() => Native.LibVLCMediaPlayerPause(handle);

        /// <summary>
        /// Stop the playback (no effect if there is no media)
        /// warning:
        /// This is synchronous, and will block until all VLC threads have been joined.
        /// Calling this from a VLC callback is a bound to cause a deadlock.
        /// </summary>
        public void Stop() => Native.LibVLCMediaPlayerStop(handle);

#if APPLE || DESKTOP
        /// <summary>
        /// Get the NSView handler previously set
        /// return the NSView handler or 0 if none where set
        /// <para></para>
        /// <para></para>
        /// Set the NSView handler where the media player should render its video output.
        /// Use the vout called "macosx".
        /// <para></para>
        /// The drawable is an NSObject that follow the
        /// VLCOpenGLVideoViewEmbedding protocol: VLCOpenGLVideoViewEmbedding NSObject
        /// Or it can be an NSView object.
        /// If you want to use it along with Qt4 see the QMacCocoaViewContainer.
        /// Then the following code should work:  { NSView *video = [[NSView
        /// alloc] init]; QMacCocoaViewContainer *container = new
        /// QMacCocoaViewContainer(video, parent);
        /// libvlc_media_player_set_nsobject(mp, video); [video release]; }
        /// You can find a live example in VLCVideoView in VLCKit.framework.
        /// </summary>
        public IntPtr NsObject
        {
            get => Native.LibVLCMediaPlayerGetNsobject(handle);
            set => Native.LibVLCMediaPlayerSetNsobject(handle, value);
        }
#endif

#if DESKTOP
        /// <summary>
        /// Set an X Window System drawable where the media player should render its video output.
        /// The call takes effect when the playback starts. If it is already started, it might need to be stopped before changes apply.
        /// If LibVLC was built without X11 output support, then this function has no effects.
        /// By default, LibVLC will capture input events on the video rendering area.
        /// Use libvlc_video_set_mouse_input() and libvlc_video_set_key_input() to disable that and deliver events to the parent window / to the application instead.
        /// By design, the X11 protocol delivers input events to only one recipient.
        /// <para></para>
        /// Warning:
        /// The application must call the XInitThreads() function from Xlib before libvlc_new(), and before any call to XOpenDisplay() directly
        /// or via any other library.Failure to call XInitThreads() will seriously impede LibVLC performance.
        /// Calling XOpenDisplay() before XInitThreads() will eventually crash the process. That is a limitation of Xlib.
        /// uint: X11 window ID
        /// </summary>
        public uint XWindow
        {
            get => Native.LibVLCMediaPlayerGetXwindow(handle);
            set => Native.LibVLCMediaPlayerSetXwindow(handle, value);
        }

        /// <summary>
        /// Set a Win32/Win64 API window handle (HWND) where the media player
        /// should render its video output. If LibVLC was built without
        /// Win32/Win64 API output support, then this has no effects.
        /// <para></para>
        /// Get the Windows API window handle (HWND) previously set
        /// </summary>
        public IntPtr Hwnd
        {
            get => Native.LibVLCMediaPlayerGetHwnd(handle);
            set => Native.LibVLCMediaPlayerSetHwnd(handle, value);
        }
#endif
        /// <summary>
        /// The movie length (in ms), or -1 if there is no media.
        /// </summary>
        public long Length => Native.LibVLCMediaPlayerGetLength(handle);

        /// <summary>
        /// Set the movie time (in ms). This has no effect if no media is being
        /// played. Not all formats and protocols support this.
        /// <para></para>
        /// Get the movie time (in ms), or -1 if there is no media.
        /// </summary>
        public long Time
        {
            get => Native.LibVLCMediaPlayerGetTime(handle);
            set => Native.LibVLCMediaPlayerSetTime(handle, value);
        }

        /// <summary>
        /// Set movie position as percentage between 0.0 and 1.0. This has no
        /// effect if playback is not enabled. This might not work depending on
        /// the underlying input format and protocol.
        /// <para></para>
        /// Get movie position as percentage between 0.0 and 1.0.
        /// </summary>
        public float Position
        {
            get => Native.LibVLCMediaPlayerGetPosition(handle);
            set => Native.LibVLCMediaPlayerSetPosition(handle, value);
        }

        /// <summary>
        /// Set the movie time. This has no effect if no media is being
        /// played. Not all formats and protocols support this.
        /// </summary>
        /// <param name="time">the movie time to seek to</param>
        public void SeekTo(TimeSpan time) => Time = (long)time.TotalMilliseconds;

        /// <summary>
        /// Set movie chapter (if applicable).
        /// <para></para>
        /// Get the movie chapter number currently playing, or -1 if there is no media.
        /// </summary>
        public int Chapter
        {
            get => Native.LibVLCMediaPlayerGetChapter(handle);
            set => Native.LibVLCMediaPlayerSetChapter(handle, value);
        }

        /// <summary>
        /// Get the number of chapters in movie, or -1.
        /// </summary>
        public int ChapterCount => Native.LibVLCMediaPlayerGetChapterCount(handle);

        /// <summary>
        /// True if the player is able to play
        /// </summary>
        public bool WillPlay => Native.LibVLCMediaPlayerWillPlay(handle) != 0;

        /// <summary>
        /// Get the number of chapters in title, or -1
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public int ChapterCountForTitle(int title) => Native.LibVLCMediaPlayerGetChapterCountForTitle(handle, title);

        /// <summary>
        /// Set movie title number to play
        /// <para></para>
        /// Get movie title number currently playing, or -1
        /// </summary>
        public int Title
        {
            get => Native.LibVLCMediaPlayerGetTitle(handle);
            set => Native.LibVLCMediaPlayerSetTitle(handle, value);
        }

        /// <summary>
        /// The title number count, or -1
        /// </summary>
        public int TitleCount => Native.LibVLCMediaPlayerGetTitleCount(handle);

        /// <summary>
        /// Set previous chapter (if applicable)
        /// </summary>
        public void PreviousChapter()
        {
            Native.LibVLCMediaPlayerPreviousChapter(handle);
        }

        /// <summary>
        /// Set next chapter (if applicable)
        /// </summary>
        public void NextChapter()
        {
            Native.LibVLCMediaPlayerNextChapter(handle);
        }

        /// <summary>
        /// Get the requested movie play rate.
        /// warning
        /// <para></para>
        /// Depending on the underlying media, the requested rate may be
        /// different from the real playback rate.
        /// </summary>
        public float Rate
        {
            get => Native.LibVLCMediaPlayerGetRate(handle);
            set => Native.LibVLCMediaPlayerSetRate(handle, value);
        }

        /// <summary>
        /// Set movie play rate
        /// </summary>
        /// <param name="rate">movie play rate to set</param>
        /// <returns>
        /// return -1 if an error was detected, 0 otherwise (but even then, it
        /// might not actually work depending on the underlying media protocol)
        /// </returns>
        public int SetRate(float rate)
        {
            return Native.LibVLCMediaPlayerSetRate(handle, rate);
        }

        /// <summary>
        /// Get the current state of the media player (playing, paused, ...)
        /// </summary>
        public VLCState State => Native.LibVLCMediaPlayerGetState(handle);

        /// <summary>
        /// Get the frames per second (fps) for this playing movie, or 0 if unspecified
        /// </summary>
        public float Fps => Native.LibVLCMediaPlayerGetFps(handle);

        /// <summary>
        /// Get the number of video outputs
        /// </summary>
        public uint VoutCount => Native.LibVLCMediaPlayerHasVout(handle);

        /// <summary>
        /// True if the media player can seek
        /// </summary>
        public bool IsSeekable => Native.LibVLCMediaPlayerIsSeekable(handle) != 0;

        /// <summary>
        /// True if the media player can pause
        /// </summary>
        public bool CanPause => Native.LibVLCMediaPlayerCanPause(handle) != 0;

        /// <summary>
        /// True if the current program is scrambled
        /// <para></para>
        /// LibVLC 2.2.0 or later
        /// </summary>
        public bool ProgramScambled => Native.LibVLCMediaPlayerProgramScrambled(handle) != 0;

        /// <summary>
        /// Display the next frame (if supported)
        /// </summary>
        public void NextFrame()
        {
            Native.LibVLCMediaPlayerNextFrame(handle);
        }

        /// <summary>
        /// Navigate through DVD Menu
        /// </summary>
        /// <param name="navigate">the Navigation mode</param>
        /// LibVLC 2.0.0 or later
        public void Navigate(uint navigate)
        {
            Native.LibVLCMediaPlayerNavigate(handle, navigate);
        }

        /// <summary>
        /// Set if, and how, the video title will be shown when media is played.
        /// </summary>
        /// <param name="position">position at which to display the title, or libvlc_position_disable to prevent the title from being displayed</param>
        /// <param name="timeout">title display timeout in milliseconds (ignored if libvlc_position_disable)</param>
        /// LibVLC 2.1.0 or later
        public void SetVideoTitleDisplay(Position position, uint timeout)
        {
            Native.LibVLCMediaPlayerSetVideoTitleDisplay(handle, position, timeout);
        }

        /// <summary>
        /// Toggle fullscreen status on non-embedded video outputs.
        /// <para></para>
        /// warning: The same limitations applies to this function as to MediaPlayer::setFullscreen()
        /// </summary>
        public void ToggleFullscreen()
        {
            Native.LibVLCToggleFullscreen(handle);
        }

        /// <summary>
        /// Enable or disable fullscreen.
        /// Warning, TL;DR version : Unless you know what you're doing, don't use this.
        /// Put your VideoView inside a fullscreen control instead, refer to your platform documentation.
        /// <para></para>
        /// Warning, long version :
        /// With most window managers, only a top-level windows can be in full-screen mode.
        /// Hence, this function will not operate properly if libvlc_media_player_set_xwindow() was used to embed the video in a non-top-level window.
        /// In that case, the embedding window must be reparented to the root window before fullscreen mode is enabled.
        /// You will want to reparent it back to its normal parent when disabling fullscreen.
        /// <para></para>
        /// return the fullscreen status (boolean)
        /// </summary>
        public bool Fullscreen
        {
            get => Native.LibVLCGetFullscreen(handle) != 0;
            set => Native.LibVLCSetFullscreen(handle, value ? 1 : 0);
        }

        /// <summary>
        /// Toggle teletext transparent status on video output.
        /// </summary>
        public void ToggleTeletext() => Native.LibVLCToggleTeletext(handle);

        /// <summary>
        /// Apply new equalizer settings to a media player.
        /// The equalizer is first created by invoking libvlc_audio_equalizer_new() or libvlc_audio_equalizer_new_from_preset().
        /// It is possible to apply new equalizer settings to a media player whether the media player is currently playing media or not.
        /// Invoking this method will immediately apply the new equalizer settings to the audio output of the currently playing media if there is any.
        /// If there is no currently playing media, the new equalizer settings will be applied later if and when new media is played.
        /// Equalizer settings will automatically be applied to subsequently played media.
        /// To disable the equalizer for a media player invoke this method passing NULL for the p_equalizer parameter.
        /// The media player does not keep a reference to the supplied equalizer so it is safe for an application to release the equalizer reference
        /// any time after this method returns.
        /// </summary>
        /// <param name="equalizer">opaque equalizer handle, or NULL to disable the equalizer for this media player</param>
        /// LibVLC 2.2.0 or later
        /// <returns>true on success, false otherwise.</returns>
        public bool SetEqualizer(Equalizer equalizer) => Native.LibVLCMediaPlayerSetEqualizer(handle, equalizer) == 0;

        /// <summary>
        /// unsetEqualizer disable equalizer for this media player
        /// </summary>
        /// <returns>true on success, false otherwise.</returns>
        public bool UnsetEqualizer() => Native.LibVLCMediaPlayerSetEqualizerPtr(handle, IntPtr.Zero) == 0;

        LibVLCAudioPlayCb _audioPlayCb;
        LibVLCAudioPauseCb _audioPauseCb;
        LibVLCAudioResumeCb _audioResumeCb;
        LibVLCAudioFlushCb _audioFlushCb;
        LibVLCAudioDrainCb _audioDrainCb;
        IntPtr _audioUserData = IntPtr.Zero;

        /// <summary>
        /// Sets callbacks and private data for decoded audio.
        /// Use libvlc_audio_set_format() or libvlc_audio_set_format_callbacks() to configure the decoded audio format.
        /// Note: The audio callbacks override any other audio output mechanism. If the callbacks are set, LibVLC will not output audio in any way.
        /// </summary>
        /// <param name="playCb">callback to play audio samples (must not be NULL) </param>
        /// <param name="pauseCb">callback to pause playback (or NULL to ignore) </param>
        /// <param name="resumeCb">callback to resume playback (or NULL to ignore) </param>
        /// <param name="flushCb">callback to flush audio buffers (or NULL to ignore) </param>
        /// <param name="drainCb">callback to drain audio buffers (or NULL to ignore) </param>
        public void SetAudioCallbacks(LibVLCAudioPlayCb playCb, LibVLCAudioPauseCb pauseCb,
            LibVLCAudioResumeCb resumeCb, LibVLCAudioFlushCb flushCb,
            LibVLCAudioDrainCb drainCb)
        {
            _audioPlayCb = playCb ?? throw new ArgumentNullException(nameof(playCb));
            _audioPauseCb = pauseCb;
            _audioResumeCb = resumeCb;
            _audioFlushCb = flushCb;
            _audioDrainCb = drainCb;

            if (!_gcHandle.IsAllocated)
            {
                _gcHandle = GCHandle.Alloc(this);
            }

            Native.LibVLCAudioSetCallbacks(
                handle,
                AudioPlayCallbackHandle,
                (pauseCb == null) ? null : AudioPauseCallbackHandle,
                (resumeCb == null) ? null : AudioResumeCallbackHandle,
                (flushCb == null) ? null : AudioFlushCallbackHandle,
                (drainCb == null) ? null : AudioDrainCallbackHandle,
                GCHandle.ToIntPtr(_gcHandle));
        }

        LibVLCVolumeCb _audioVolumeCb;

        /// <summary>
        /// Set callbacks and private data for decoded audio.
        /// This only works in combination with libvlc_audio_set_callbacks().
        /// Use libvlc_audio_set_format() or libvlc_audio_set_format_callbacks() to configure the decoded audio format.
        /// </summary>
        /// <param name="volumeCb">callback to apply audio volume, or NULL to apply volume in software</param>
        public void SetVolumeCallback(LibVLCVolumeCb volumeCb)
        {
            _audioVolumeCb = volumeCb;
            Native.LibVLCAudioSetVolumeCallback(handle, (volumeCb == null) ? null : AudioVolumeCallbackHandle);
        }

        LibVLCAudioSetupCb _setupCb;
        LibVLCAudioCleanupCb _cleanupCb;

        /// <summary>
        /// Sets decoded audio format via callbacks.
        /// This only works in combination with libvlc_audio_set_callbacks().
        /// </summary>
        /// <param name="setupCb">callback to select the audio format (cannot be NULL)</param>
        /// <param name="cleanupCb">callback to release any allocated resources (or NULL)</param>
        public void SetAudioFormatCallback(LibVLCAudioSetupCb setupCb, LibVLCAudioCleanupCb cleanupCb)
        {
            _setupCb = setupCb ?? throw new ArgumentNullException(nameof(setupCb));
            _cleanupCb = cleanupCb;
            Native.LibVLCAudioSetFormatCallbacks(handle, AudioSetupCallbackHandle, (cleanupCb == null) ? null : AudioCleanupCallbackHandle);
        }

        /// <summary>
        /// Sets a fixed decoded audio format.
        /// This only works in combination with libvlc_audio_set_callbacks(), and is mutually exclusive with libvlc_audio_set_format_callbacks().
        /// </summary>
        /// <param name="format">a four-characters string identifying the sample format (e.g. "S16N" or "FL32")</param>
        /// <param name="rate">sample rate (expressed in Hz)</param>
        /// <param name="channels">channels count</param>
        public void SetAudioFormat(string format, uint rate, uint channels)
        {
            var formatUtf8 = format.ToUtf8();
            MarshalUtils.PerformInteropAndFree(() => Native.LibVLCAudioSetFormat(handle, formatUtf8, rate, channels), formatUtf8);
        }

        /// <summary>
        /// Selects an audio output module.
        /// Note:
        /// Any change will take effect only after playback is stopped and restarted. Audio output cannot be changed while playing.
        /// </summary>
        /// <param name="name">name of audio output, use psz_name of</param>
        /// <returns>true if function succeeded, false on error</returns>
        public bool SetAudioOutput(string name)
        {
            var nameUtf8 = name.ToUtf8();
            return MarshalUtils.PerformInteropAndFree(() => Native.LibVLCAudioOutputSet(handle, nameUtf8), nameUtf8) == 0;
        }

        /// <summary>
        /// Get the current audio output device identifier.
        /// This complements <see cref="SetOutputDevice"/>
        /// warning The initial value for the current audio output device identifier
        /// may not be set or may be some unknown value.A LibVLC application should
        /// compare this value against the known device identifiers (e.g.those that
        /// were previously retrieved by a call to <see cref="AudioOutputDeviceEnum"/> or
        /// <see cref="LibVLC.AudioOutputDevices"/>) to find the current audio output device.
        ///
        /// It is possible that the selected audio output device changes(an external
        /// change) without a call to <see cref="SetOutputDevice"/>.That may make this
        /// method unsuitable to use if a LibVLC application is attempting to track
        /// dynamic audio device changes as they happen.
        ///
        /// </summary>
        /// <returns>the current audio output device identifier, or NULL if no device is selected or in case of error.</returns>
        public string OutputDevice => Native.LibVLCAudioOutputDeviceGet(handle).FromUtf8(libvlcFree: true);

        /// <summary>
        /// Configures an explicit audio output device.
        /// If the module paramater is NULL, audio output will be moved to the device
        /// specified by the device identifier string immediately.This is the
        /// recommended usage.
        /// A list of adequate potential device strings can be obtained with
        /// <see cref="AudioOutputDeviceEnum"/>
        /// However passing NULL is supported in LibVLC version 2.2.0 and later only;
        /// in earlier versions, this function would have no effects when the module
        /// parameter was NULL.
        /// If the module parameter is not NULL, the device parameter of the
        /// corresponding audio output, if it exists, will be set to the specified
        /// string.
        /// A list of adequate potential device strings can be obtained with
        /// <see cref="LibVLC.AudioOutputDevices"/>
        /// </summary>
        /// <param name="deviceId">device identifier string</param>
        /// <param name="module">If NULL, current audio output module. if non-NULL, name of audio output module</param>
        public void SetOutputDevice(string deviceId, string module = null)
        {
            var deviceIdUtf8 = deviceId.ToUtf8();
            var moduleUtf8 = module.ToUtf8();
            MarshalUtils.PerformInteropAndFree(() =>
                Native.LibVLCAudioOutputDeviceSet(handle, moduleUtf8, deviceIdUtf8),
                moduleUtf8, deviceIdUtf8);
        }

        /// <summary>
        /// Gets a list of potential audio output devices
        /// <para/> Not all audio outputs support enumerating devices. The audio output may be functional even if the list is empty (NULL).
        /// The list may not be exhaustive. Some audio output devices in the list might not actually work in some circumstances.
        /// <para/> By default, it is recommended to not specify any explicit audio device.
        /// </summary>
        public AudioOutputDevice[] AudioOutputDeviceEnum =>
           MarshalUtils.Retrieve(() => Native.LibVLCAudioOutputDeviceEnum(handle),
           MarshalUtils.PtrToStructure<AudioOutputDeviceStructure>,
           s => s.Build(),
           device => device.Next,
           Native.LibVLCAudioOutputDeviceListRelease);

        /// <summary>
        /// Toggle mute status.
        /// Warning
        /// Toggling mute atomically is not always possible: On some platforms, other processes can mute the VLC audio playback
        /// stream asynchronously.
        /// Thus, there is a small race condition where toggling will not work.
        /// See also the limitations of libvlc_audio_set_mute().
        /// </summary>
        public void ToggleMute() => Native.LibVLCAudioToggleMute(handle);

        /// <summary>
        /// Get current mute status.
        /// Set mute status.
        /// Warning
        /// This function does not always work.
        /// If there are no active audio playback stream, the mute status might not be available.
        /// If digital pass-through (S/PDIF, HDMI...) is in use, muting may be unapplicable.
        /// Also some audio output plugins do not support muting at all.
        /// Note
        /// To force silent playback, disable all audio tracks. This is more efficient and reliable than mute.
        /// </summary>
        public bool Mute
        {
            get => Native.LibVLCAudioGetMute(handle) == 1;
            set => Native.LibVLCAudioSetMute(handle, value ? 1 : 0);
        }

        /// <summary>
        /// Get/Set the volume in percents (0 = mute, 100 = 0dB)
        /// </summary>
        public int Volume
        {
            get => Native.LibVLCAudioGetVolume(handle);
            set => Native.LibVLCAudioSetVolume(handle, value);
        }

        /// <summary>
        /// Get the number of available audio tracks (int), or -1 if unavailable
        /// </summary>
        public int AudioTrackCount => Native.LibVLCAudioGetTrackCount(handle);

        /// <summary>
        /// Retrive the audio track description
        /// </summary>
        public TrackDescription[] AudioTrackDescription => MarshalUtils.Retrieve(() => Native.LibVLCAudioGetTrackDescription(handle),
            MarshalUtils.PtrToStructure<TrackDescriptionStructure>,
            t => t.Build(),
            t => t.Next,
            Native.LibVLCTrackDescriptionListRelease);

        /// <summary>
        /// Get current audio track ID or -1 if no active input.
        /// </summary>
        public int AudioTrack => Native.LibVLCAudioGetTrack(handle);

        /// <summary>
        /// Set current audio track.
        /// </summary>
        /// <param name="trackIndex">the track ID (i_id field from track description)</param>
        /// <returns>true on success, false on error</returns>
        public bool SetAudioTrack(int trackIndex) => Native.LibVLCAudioSetTrack(handle, trackIndex) == 0;

        /// <summary>
        /// Get current audio channel.
        /// </summary>
        public AudioOutputChannel Channel => Native.LibVLCAudioGetChannel(handle);

        /// <summary>
        /// Set current audio channel.
        /// </summary>
        /// <param name="channel">the audio channel</param>
        /// <returns></returns>
        public bool SetChannel(AudioOutputChannel channel) => Native.LibVLCAudioSetChannel(handle, channel) == 0;

        /// <summary>
        /// Equals override based on the native instance reference
        /// </summary>
        /// <param name="obj">the mediaplayer instance to compare this to</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is MediaPlayer player &&
                   EqualityComparer<IntPtr>.Default.Equals(handle, player.handle);
        }

        /// <summary>
        /// Custom hascode implemenation for this MediaPlayer instance
        /// </summary>
        /// <returns>the hashcode for this MediaPlayer instance</returns>
        public override int GetHashCode()
        {
            return handle.GetHashCode();
        }

        /// <summary>
        /// Get current audio delay (microseconds).
        /// </summary>
        public long AudioDelay => Native.LibVLCAudioGetDelay(handle);

        /// <summary>
        /// Set current audio delay. The audio delay will be reset to zero each
        /// time the media changes.
        /// </summary>
        /// <param name="delay">the audio delay (microseconds)</param>
        /// <returns>true on success, false on error </returns>
        public bool SetAudioDelay(long delay) => Native.LibVLCAudioSetDelay(handle, delay) == 0;

        LibVLCVideoLockCb _videoLockCb;
        LibVLCVideoUnlockCb _videoUnlockCb;
        LibVLCVideoDisplayCb _videoDisplayCb;

        /// <summary>
        /// Set callbacks and private data to render decoded video to a custom area in memory.
        /// Use libvlc_video_set_format() or libvlc_video_set_format_callbacks() to configure the decoded format.
        /// Warning
        /// Rendering video into custom memory buffers is considerably less efficient than rendering in a custom window as normal.
        /// For optimal perfomances, VLC media player renders into a custom window, and does not use this function and associated callbacks.
        /// It is highly recommended that other LibVLC-based application do likewise.
        /// To embed video in a window, use libvlc_media_player_set_xid() or equivalent depending on the operating system.
        /// If window embedding does not fit the application use case, then a custom LibVLC video output display plugin is required to maintain optimal video rendering performances.
        /// The following limitations affect performance:
        /// Hardware video decoding acceleration will either be disabled completely, or require(relatively slow) copy from video/DSP memory to main memory.
        /// Sub-pictures(subtitles, on-screen display, etc.) must be blent into the main picture by the CPU instead of the GPU.
        /// Depending on the video format, pixel format conversion, picture scaling, cropping and/or picture re-orientation,
        /// must be performed by the CPU instead of the GPU.
        /// Memory copying is required between LibVLC reference picture buffers and application buffers (between lock and unlock callbacks).
        /// </summary>
        /// <param name="lockCb">callback to lock video memory (must not be NULL)</param>
        /// <param name="unlockCb">callback to unlock video memory (or NULL if not needed)</param>
        /// <param name="displayCb">callback to display video (or NULL if not needed)</param>
        public void SetVideoCallbacks(LibVLCVideoLockCb lockCb, LibVLCVideoUnlockCb unlockCb,
            LibVLCVideoDisplayCb displayCb)
        {
            _videoLockCb = lockCb ?? throw new ArgumentNullException(nameof(lockCb));
            _videoUnlockCb = unlockCb;
            _videoDisplayCb = displayCb;

            if (!_gcHandle.IsAllocated)
            {
                _gcHandle = GCHandle.Alloc(this);
            }

            Native.LibVLCVideoSetCallbacks(handle,
                                           VideoLockCallbackHandle,
                                           (unlockCb == null) ? null : VideoUnlockCallbackHandle,
                                           (displayCb == null) ? null : VideoDisplayCallbackHandle,
                                           GCHandle.ToIntPtr(_gcHandle));
        }

        /// <summary>
        /// Set decoded video chroma and dimensions. This only works in
        /// combination with MediaPlayer::setCallbacks() , and is mutually exclusive
        /// with MediaPlayer::setFormatCallbacks()
        /// </summary>
        /// <param name="chroma">a four-characters string identifying the chroma (e.g."RV32" or "YUYV")</param>
        /// <param name="width">pixel width</param>
        /// <param name="height">pixel height</param>
        /// <param name="pitch">line pitch (in bytes)</param>
        public void SetVideoFormat(string chroma, uint width, uint height, uint pitch)
        {
            var chromaUtf8 = chroma.ToUtf8();

            MarshalUtils.PerformInteropAndFree(() =>
                Native.LibVLCVideoSetFormat(handle, chromaUtf8, width, height, pitch),
                chromaUtf8);
        }

        LibVLCVideoFormatCb _videoFormatCb;
        LibVLCVideoCleanupCb _videoCleanupCb;
        IntPtr _videoUserData = IntPtr.Zero;

        /// <summary>
        /// Set decoded video chroma and dimensions.
        /// This only works in combination with libvlc_video_set_callbacks().
        /// </summary>
        /// <param name="formatCb">callback to select the video format (cannot be NULL)</param>
        /// <param name="cleanupCb">callback to release any allocated resources (or NULL)</param>
        public void SetVideoFormatCallbacks(LibVLCVideoFormatCb formatCb, LibVLCVideoCleanupCb cleanupCb)
        {
            _videoFormatCb = formatCb ?? throw new ArgumentNullException(nameof(formatCb));
            _videoCleanupCb = cleanupCb;
            Native.LibVLCVideoSetFormatCallbacks(handle, VideoFormatCallbackHandle,
                (cleanupCb == null) ? null : _videoCleanupCb);
        }

        /// <summary>
        /// Enable or disable key press events handling, according to the LibVLC hotkeys configuration.
        /// By default and for historical reasons, keyboard events are handled by the LibVLC video widget.
        /// Note
        /// On X11, there can be only one subscriber for key press and mouse click events per window.
        /// If your application has subscribed to those events for the X window ID of the video widget,
        /// then LibVLC will not be able to handle key presses and mouse clicks in any case.
        /// Warning
        /// This function is only implemented for X11 and Win32 at the moment.
        /// true to handle key press events, false to ignore them.
        /// </summary>
        public bool EnableKeyInput
        {
            set => Native.LibVLCVideoSetKeyInput(handle, value ? 1 : 0);
        }

        /// <summary>
        /// Enable or disable mouse click events handling.
        /// By default, those events are handled. This is needed for DVD menus to work, as well as a few video filters such as "puzzle".
        /// Warning
        /// This function is only implemented for X11 and Win32 at the moment.
        /// true to handle mouse click events, false to ignore them.
        /// </summary>
        public bool EnableMouseInput
        {
            set => Native.LibVLCVideoSetMouseInput(handle, value ? 1 : 0);
        }

        /// <summary>
        /// Get the pixel dimensions of a video.
        /// </summary>
        /// <param name="num">number of the video (starting from, and most commonly 0)</param>
        /// <param name="px">pointer to get the pixel width [OUT]</param>
        /// <param name="py">pointer to get the pixel height [OUT]</param>
        /// <returns></returns>
        public bool Size(uint num, ref uint px, ref uint py)
        {
            return Native.LibVLCVideoGetSize(handle, num, ref px, ref py) == 0;
        }

        /// <summary>
        /// Get the mouse pointer coordinates over a video.
        /// Coordinates are expressed in terms of the decoded video resolution, not in terms of pixels on the screen/viewport
        /// (to get the latter, you can query your windowing system directly).
        /// Either of the coordinates may be negative or larger than the corresponding dimension of the video,
        /// if the cursor is outside the rendering area.
        /// Warning
        /// The coordinates may be out-of-date if the pointer is not located on the video rendering area.
        /// LibVLC does not track the pointer if it is outside of the video widget.
        /// Note
        /// LibVLC does not support multiple pointers(it does of course support multiple input devices sharing the same pointer) at the moment.
        /// </summary>
        /// <param name="num">number of the video (starting from, and most commonly 0)</param>
        /// <param name="px">pointer to get the abscissa [OUT]</param>
        /// <param name="py">pointer to get the ordinate [OUT]</param>
        /// <returns>true on success, false on failure</returns>
        public bool Cursor(uint num, ref int px, ref int py)
        {
            return Native.LibVLCVideoGetCursor(handle, num, ref px, ref py) == 0;
        }

        /// <summary>
        /// Get/Set the current video scaling factor. See also MediaPlayer::setScale() .
        /// That is the ratio of the number of
        /// pixels on screen to the number of pixels in the original decoded video
        /// in each dimension.Zero is a special value; it will adjust the video
        /// to the output window/drawable(in windowed mode) or the entire screen.
        /// Note that not all video outputs support scaling.
        /// </summary>
        public float Scale
        {
            get => Native.LibVLCVideoGetScale(handle);
            set => Native.LibVLCVideoSetScale(handle, value);
        }

        /// <summary>
        /// Get/set current video aspect ratio.
        /// Set to null to reset to default
        /// Invalid aspect ratios are ignored.
        /// </summary>
        public string AspectRatio
        {
            get => Native.LibVLCVideoGetAspectRatio(handle).FromUtf8(libvlcFree: true);
            set
            {
                var aspectRatioUtf8 = value.ToUtf8();
                MarshalUtils.PerformInteropAndFree(() => Native.LibVLCVideoSetAspectRatio(handle, aspectRatioUtf8), aspectRatioUtf8);
            }
        }

        /// <summary>
        /// The current video subtitle track
        /// </summary>
        public int Spu => Native.LibVLCVideoGetSpu(handle);

        /// <summary>
        /// Set Spu (subtitle)
        /// </summary>
        /// <param name="spu">Video subtitle track to select (id from track description)</param>
        /// <returns>true on success, false otherwise</returns>
        public bool SetSpu(int spu) => Native.LibVLCVideoSetSpu(handle, spu) == 0;

        /// <summary>
        /// Get the number of available video subtitles.
        /// </summary>
        public int SpuCount => Native.LibVLCVideoGetSpuCount(handle);

        /// <summary>
        /// Retrieve SpuDescription in a TrackDescription struct
        /// </summary>
        public TrackDescription[] SpuDescription => MarshalUtils.Retrieve(() => Native.LibVLCVideoGetSpuDescription(handle),
            MarshalUtils.PtrToStructure<TrackDescriptionStructure>,
            t => t.Build(),
            t => t.Next,
            Native.LibVLCTrackDescriptionListRelease);

        /// <summary>
        /// Get the current subtitle delay.
        /// </summary>
        public long SpuDelay => Native.LibVLCVideoGetSpuDelay(handle);

        /// <summary>
        /// Set the subtitle delay.
        /// This affects the timing of when the subtitle will be displayed.
        /// Positive values result in subtitles being displayed later, while negative values will result in subtitles being displayed earlier.
        /// The subtitle delay will be reset to zero each time the media changes.
        /// </summary>
        /// <param name="delay">time (in microseconds) the display of subtitles should be delayed</param>
        /// <returns>true if successful, false otherwise</returns>
        public bool SetSpuDelay(long delay) => Native.LibVLCVideoSetSpuDelay(handle, delay) == 0;

        /// <summary>
        /// Get the description of available titles.
        /// </summary>
        public TrackDescription[] TitleDescription => MarshalUtils.Retrieve(() => Native.LibVLCVideoGetTitleDescription(handle),
            MarshalUtils.PtrToStructure<TrackDescriptionStructure>,
            t => t.Build(),
            t => t.Next,
            Native.LibVLCTrackDescriptionListRelease);

        /// <summary>
        /// Get the full description of available chapters.
        /// </summary>
        /// <param name="titleIndex">Index of the title to query for chapters (uses current title if set to -1)</param>
        /// <returns>Array of chapter descriptions.</returns>
        public ChapterDescription[] FullChapterDescriptions(int titleIndex = -1) => MarshalUtils.Retrieve(handle,
            (IntPtr nativeRef, out IntPtr array) =>
            {
                var count = Native.LibVLCMediaPlayerGetFullChapterDescriptions(nativeRef, titleIndex, out array);
                // the number of chapters (-1 on error)
                return count < 0 ? 0 : (uint)count;
            },
            MarshalUtils.PtrToStructure<ChapterDescriptionStructure>,
            t => t.Build(),
            Native.LibVLCChapterDescriptionsRelease);

        /// <summary>
        /// Get the description of available chapters for specific title.
        /// </summary>
        /// <param name="titleIndex">selected title</param>
        /// <returns>chapter descriptions</returns>
        public TrackDescription[] ChapterDescription(int titleIndex) => MarshalUtils.Retrieve(() => Native.LibVLCVideoGetChapterDescription(handle, titleIndex),
            MarshalUtils.PtrToStructure<TrackDescriptionStructure>,
            t => t.Build(),
            t => t.Next,
            Native.LibVLCTrackDescriptionListRelease);

        /// <summary>
        /// Get/Set current crop filter geometry.
        /// Empty string to unset
        /// </summary>
        public string CropGeometry
        {
            get => Native.LibVLCVideoGetCropGeometry(handle).FromUtf8(libvlcFree: true);
            set
            {
                var cropGeometryUtf8 = value.ToUtf8();
                MarshalUtils.PerformInteropAndFree(() => Native.LibVLCVideoSetCropGeometry(handle, cropGeometryUtf8), cropGeometryUtf8);
            }
        }

        /// <summary>
        /// Get current teletext page requested.
        /// Set new teletext page to retrieve.
        /// </summary>
        public int Teletext
        {
            get => Native.LibVLCVideoGetTeletext(handle);
            set => Native.LibVLCVideoSetTeletext(handle, value);
        }

        /// <summary>
        /// Get number of available video tracks.
        /// </summary>
        public int VideoTrackCount => Native.LibVLCVideoGetTrackCount(handle);

        /// <summary>
        /// Get the description of available video tracks.
        /// </summary>
        public TrackDescription[] VideoTrackDescription => MarshalUtils.Retrieve(() => Native.LibVLCVideoGetTrackDescription(handle),
            MarshalUtils.PtrToStructure<TrackDescriptionStructure>,
            t => t.Build(),
            t => t.Next,
            Native.LibVLCTrackDescriptionListRelease);

        /// <summary>
        /// Get current video track ID (int) or -1 if no active input.
        /// </summary>
        public int VideoTrack => Native.LibVLCVideoGetTrack(handle);

        /// <summary>
        /// Set video track.
        /// </summary>
        /// <param name="trackIndex">the track ID (i_id field from track description)</param>
        /// <returns>true on success, false out of range</returns>
        public bool SetVideoTrack(int trackIndex) => Native.LibVLCVideoSetTrack(handle, trackIndex) == 0;

        /// <summary>
        /// Take a snapshot of the current video window.
        /// If i_width AND i_height is 0, original size is used. If i_width XOR
        /// i_height is 0, original aspect-ratio is preserved.
        /// </summary>
        /// <param name="num">number of video output (typically 0 for the first/only one)</param>
        /// <param name="filePath">the path where to save the screenshot to</param>
        /// <param name="width">the snapshot's width</param>
        /// <param name="height">the snapshot's height</param>
        /// <returns>true on success</returns>
        public bool TakeSnapshot(uint num, string filePath, uint width, uint height)
        {
            var filePathUtf8 = filePath.ToUtf8();
            return MarshalUtils.PerformInteropAndFree(() =>
                Native.LibVLCVideoTakeSnapshot(handle, num, filePathUtf8, width, height) == 0,
                filePathUtf8);
        }

        /// <summary>
        /// Enable or disable deinterlace filter
        /// </summary>
        /// <param name="deinterlaceMode">type of deinterlace filter, null to disable</param>
        public void SetDeinterlace(string deinterlaceMode)
        {
            var deinterlaceModeUtf8 = deinterlaceMode.ToUtf8();

            MarshalUtils.PerformInteropAndFree(() =>
                Native.LibVLCVideoSetDeinterlace(handle, deinterlaceModeUtf8),
                deinterlaceModeUtf8);
        }

        /// <summary>
        /// Get an integer marquee option value
        /// </summary>
        /// <param name="option">marq option to get</param>
        /// <returns></returns>
        public int MarqueeInt(VideoMarqueeOption option) => Native.LibVLCVideoGetMarqueeInt(handle, option);

        /// <summary>
        /// Get a string marquee option value
        /// </summary>
        /// <param name="option">marq option to get</param>
        /// <returns></returns>
        public string MarqueeString(VideoMarqueeOption option)
        {
            var marqueeStrPtr = Native.LibVLCVideoGetMarqueeString(handle, option);
            return marqueeStrPtr.FromUtf8(libvlcFree: true);
        }

        /// <summary>
        /// Enable, disable or set an integer marquee option
        /// Setting libvlc_marquee_Enable has the side effect of enabling (arg !0)
        /// or disabling (arg 0) the marq filter.
        /// </summary>
        /// <param name="option">marq option to set</param>
        /// <param name="value">marq option value</param>
        public void SetMarqueeInt(VideoMarqueeOption option, int value) =>
            Native.LibVLCVideoSetMarqueeInt(handle, option, value);

        /// <summary>
        /// Enable, disable or set an string marquee option
        /// </summary>
        /// <param name="option">marq option to set</param>
        /// <param name="marqueeValue">marq option value</param>
        public void SetMarqueeString(VideoMarqueeOption option, string marqueeValue)
        {
            var marqueeValueUtf8 = marqueeValue.ToUtf8();
            MarshalUtils.PerformInteropAndFree(() =>
                Native.LibVLCVideoSetMarqueeString(handle, option, marqueeValueUtf8),
                marqueeValueUtf8);
        }


        /// <summary>
        /// Get integer logo option.
        /// </summary>
        /// <param name="option">logo option to get, values of libvlc_video_logo_option_t</param>
        /// <returns></returns>
        public int LogoInt(VideoLogoOption option) => Native.LibVLCVideoGetLogoInt(handle, option);

        /// <summary>
        /// Set logo option as integer. Options that take a different type value
        /// are ignored. Passing libvlc_logo_enable as option value has the side
        /// effect of starting (arg !0) or stopping (arg 0) the logo filter.
        /// </summary>
        /// <param name="option">logo option to set, values of libvlc_video_logo_option_t</param>
        /// <param name="value">logo option value</param>
        public void SetLogoInt(VideoLogoOption option, int value) => Native.LibVLCVideoSetLogoInt(handle, option, value);

        /// <summary>
        /// Set logo option as string. Options that take a different type value are ignored.
        /// </summary>
        /// <param name="option">logo option to set, values of libvlc_video_logo_option_t</param>
        /// <param name="logoValue">logo option value</param>
        public void SetLogoString(VideoLogoOption option, string logoValue)
        {
            var logoValueUtf8 = logoValue.ToUtf8();

            MarshalUtils.PerformInteropAndFree(() =>
                Native.LibVLCVideoSetLogoString(handle, option, logoValueUtf8),
                logoValueUtf8);
        }

        /// <summary>
        /// Get integer adjust option.
        /// </summary>
        /// <param name="option">adjust option to get, values of libvlc_video_adjust_option_t</param>
        /// <returns></returns>
        public int AdjustInt(VideoAdjustOption option) => Native.LibVLCVideoGetAdjustInt(handle, option);

        /// <summary>
        /// Set adjust option as integer. Options that take a different type value
        /// are ignored. Passing libvlc_adjust_enable as option value has the side
        /// effect of starting (arg !0) or stopping (arg 0) the adjust filter.
        /// </summary>
        /// <param name="option">adust option to set, values of libvlc_video_adjust_option_t</param>
        /// <param name="value">adjust option value</param>
        public void SetAdjustInt(VideoAdjustOption option, int value) => Native.LibVLCVideoSetAdjustInt(handle, option, value);

        /// <summary>
        /// Get adjust option float value
        /// </summary>
        /// <param name="option">The option for which to get the value</param>
        /// <returns>the float value for a given option</returns>
        public float AdjustFloat(VideoAdjustOption option) => Native.LibVLCVideoGetAdjustFloat(handle, option);

        /// <summary>
        /// Set adjust option as float. Options that take a different type value are ignored.
        /// </summary>
        /// <param name="option">adust option to set, values of <see cref="VideoAdjustOption"/></param>
        /// <param name="value">adjust option value</param>
        public void SetAdjustFloat(VideoAdjustOption option, float value) => Native.LibVLCVideoSetAdjustFloat(handle, option, value);

#if ANDROID
        /// <summary>
        /// Set the android context.
        /// </summary>
        /// <param name="aWindow">See LibVLCSharp.Android</param>
        public void SetAndroidContext(IntPtr aWindow) => Native.LibVLCMediaPlayerSetAndroidContext(handle, aWindow);
#endif

        /// <summary>
        /// Add a slave to the current media player.
        /// note If the player is playing, the slave will be added directly. This call
        /// will also update the slave list of the attached VLC::Media.
        /// </summary>
        /// <param name="type">subtitle or audio</param>
        /// <param name="uri">Uri of the slave (should contain a valid scheme).</param>
        /// <param name="select">True if this slave should be selected when it's loaded</param>
        /// <returns></returns>
        public bool AddSlave(MediaSlaveType type, string uri, bool select)
        {
            var uriUtf8 = uri.ToUtf8();
            return MarshalUtils.PerformInteropAndFree(() =>
                Native.LibVLCMediaPlayerAddSlave(handle, type, uriUtf8, select) == 0,
                uriUtf8);
        }

        /// <summary>
        /// Current 360 viewpoint of this mediaplayer.
        /// <para/>Update with <see cref="UpdateViewpoint"/>
        /// </summary>
        public VideoViewpoint Viewpoint { get; private set; }

        /// <summary>
        /// Update the video viewpoint information.
        /// The values are set asynchronously, it will be used by the next frame displayed.
        /// It is safe to call this function before the media player is started.
        /// LibVLC 3.0.0 and later
        /// </summary>
        /// <param name="yaw">view point yaw in degrees  ]-180;180]</param>
        /// <param name="pitch">view point pitch in degrees  ]-90;90]</param>
        /// <param name="roll">view point roll in degrees ]-180;180]</param>
        /// <param name="fov">field of view in degrees ]0;180[ (default 80.)</param>
        /// <param name="absolute">if true replace the old viewpoint with the new one. If false, increase/decrease it.</param>
        /// <returns>true if successful, false otherwise</returns>
        public bool UpdateViewpoint(float yaw, float pitch, float roll, float fov, bool absolute = true)
        {
            var vpPtr = Native.LibVLCVideoNewViewpoint();
            if (vpPtr == IntPtr.Zero) return false;

            Viewpoint = new VideoViewpoint(yaw, pitch, roll, fov);
            Marshal.StructureToPtr(Viewpoint, vpPtr, false);

            var result = Native.LibVLCVideoUpdateViewpoint(handle, vpPtr, absolute) == 0;
            MarshalUtils.LibVLCFree(ref vpPtr);

            return result;
        }

        /// <summary>
        /// Set a renderer to the media player.
        /// </summary>
        /// <param name="rendererItem">discovered renderer item or null to fallback on local rendering</param>
        /// <returns>true on success, false otherwise</returns>
        //public bool SetRenderer(RendererItem rendererItem) =>
        //    Native.LibVLCMediaPlayerSetRenderer(handle, rendererItem?.handle ?? IntPtr.Zero) == 0;
        public bool SetRenderer(RendererItem rendererItem)
        {
            if (rendererItem != null)
            {
                return Native.LibVLCMediaPlayerSetRenderer(handle, rendererItem) == 0;
            }

            return Native.LibVLCMediaPlayerSetRendererPtr(handle, IntPtr.Zero) == 0;
        }

        /// <summary>Gets the media role.
        /// <para/> version LibVLC 3.0.0 and later.
        /// </summary>
        public MediaPlayerRole Role => Native.LibVLCMediaPlayerGetRole(handle);

        /// <summary>Sets the media role.
        /// <para/> version LibVLC 3.0.0 and later.
        /// </summary>
        /// <returns>true on success, false otherwise</returns>
        public bool SetRole(MediaPlayerRole role) => Native.LibVLCMediaPlayerSetRole(handle, role) == 0;

        /// <summary>Increments the native reference counter for this mediaplayer instance</summary>
        internal void Retain() => Native.LibVLCMediaPlayerRetain(handle);

        /// <summary>
        /// Enable/disable hardware decoding in a crossplatform way.
        /// </summary>
        public bool EnableHardwareDecoding
        {
            get => Configuration.EnableHardwareDecoding;
            set => Configuration.EnableHardwareDecoding = value;
        }

        /// <summary>
        /// Caching value for local files, in milliseconds [0 .. 60000ms]
        /// </summary>
        public uint FileCaching
        {
            get => Configuration.FileCaching;
            set => Configuration.FileCaching = value;
        }

        /// <summary>
        /// Caching value for network resources, in milliseconds [0 .. 60000ms]
        /// </summary>
        public uint NetworkCaching
        {
            get => Configuration.NetworkCaching;
            set => Configuration.NetworkCaching = value;
        }

        MediaConfiguration Configuration = new MediaConfiguration();

        #region Callbacks

        static readonly LibVLCVideoLockCb VideoLockCallbackHandle = VideoLockCallback;
        static readonly LibVLCVideoUnlockCb VideoUnlockCallbackHandle = VideoUnlockCallback;
        static readonly LibVLCVideoDisplayCb VideoDisplayCallbackHandle = VideoDisplayCallback;
        static readonly LibVLCVideoFormatCb VideoFormatCallbackHandle = VideoFormatCallback;

        [MonoPInvokeCallback(typeof(LibVLCVideoLockCb))]
        private static IntPtr VideoLockCallback(IntPtr opaque, IntPtr planes)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(opaque);
            if (mediaPlayer?._videoLockCb != null)
            {
                return mediaPlayer._videoLockCb(mediaPlayer._videoUserData, planes);
            }
            return IntPtr.Zero;
        }

        [MonoPInvokeCallback(typeof(LibVLCVideoUnlockCb))]
        private static void VideoUnlockCallback(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(opaque);
            if (mediaPlayer?._videoUnlockCb != null)
            {
                mediaPlayer._videoUnlockCb(mediaPlayer._videoUserData, picture, planes);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCVideoDisplayCb))]
        private static void VideoDisplayCallback(IntPtr opaque, IntPtr picture)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(opaque);
            if (mediaPlayer?._videoDisplayCb != null)
            {
                mediaPlayer._videoDisplayCb(mediaPlayer._videoUserData, picture);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCVideoFormatCb))]
        private static uint VideoFormatCallback(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(opaque);
            if (mediaPlayer?._videoFormatCb != null)
            {
                return mediaPlayer._videoFormatCb(ref mediaPlayer._videoUserData, chroma, ref width, ref height, ref pitches, ref lines);
            }

            return 0;
        }

        static readonly LibVLCAudioPlayCb AudioPlayCallbackHandle = AudioPlayCallback;
        static readonly LibVLCAudioPauseCb AudioPauseCallbackHandle = AudioPauseCallback;
        static readonly LibVLCAudioResumeCb AudioResumeCallbackHandle = AudioResumeCallback;
        static readonly LibVLCAudioFlushCb AudioFlushCallbackHandle = AudioFlushCallback;
        static readonly LibVLCAudioDrainCb AudioDrainCallbackHandle = AudioDrainCallback;
        static readonly LibVLCVolumeCb AudioVolumeCallbackHandle = AudioVolumeCallback;
        static readonly LibVLCAudioSetupCb AudioSetupCallbackHandle = AudioSetupCallback;
        static readonly LibVLCAudioCleanupCb AudioCleanupCallbackHandle = AudioCleanupCallback;

        [MonoPInvokeCallback(typeof(LibVLCAudioPlayCb))]
        private static void AudioPlayCallback(IntPtr data, IntPtr samples, uint count, long pts)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(data);
            if (mediaPlayer?._audioPlayCb != null)
            {
                mediaPlayer._audioPlayCb(mediaPlayer._audioUserData, samples, count, pts);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCAudioPauseCb))]
        private static void AudioPauseCallback(IntPtr data, long pts)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(data);
            if (mediaPlayer?._audioPauseCb != null)
            {
                mediaPlayer._audioPauseCb(mediaPlayer._audioUserData, pts);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCAudioResumeCb))]
        private static void AudioResumeCallback(IntPtr data, long pts)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(data);
            if (mediaPlayer?._audioResumeCb != null)
            {
                mediaPlayer._audioResumeCb(mediaPlayer._audioUserData, pts);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCAudioFlushCb))]
        private static void AudioFlushCallback(IntPtr data, long pts)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(data);
            if (mediaPlayer?._audioFlushCb != null)
            {
                mediaPlayer._audioFlushCb(mediaPlayer._audioUserData, pts);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCAudioDrainCb))]
        private static void AudioDrainCallback(IntPtr data)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(data);
            if (mediaPlayer?._audioDrainCb != null)
            {
                mediaPlayer._audioDrainCb(mediaPlayer._audioUserData);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCVolumeCb))]
        private static void AudioVolumeCallback(IntPtr data, float volume, bool mute)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(data);
            if (mediaPlayer?._audioVolumeCb != null)
            {
                mediaPlayer._audioVolumeCb(mediaPlayer._audioUserData, volume, mute);
            }
        }

        [MonoPInvokeCallback(typeof(LibVLCAudioSetupCb))]
        private static int AudioSetupCallback(ref IntPtr opaque, ref IntPtr format, ref uint rate, ref uint channels)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(opaque);
            if (mediaPlayer?._setupCb != null)
            {
                return mediaPlayer._setupCb(ref mediaPlayer._audioUserData, ref format, ref rate, ref channels);
            }

            return -1;
        }

        [MonoPInvokeCallback(typeof(LibVLCAudioCleanupCb))]
        private static void AudioCleanupCallback(IntPtr opaque)
        {
            var mediaPlayer = MarshalUtils.GetInstance<MediaPlayer>(opaque);
            if (mediaPlayer?._cleanupCb != null)
            {
                mediaPlayer._cleanupCb(mediaPlayer._audioUserData);
            }
        }

        /// <summary>
        /// <para>A LibVLC media player plays one media (usually in a custom drawable).</para>
        /// <para>@{</para>
        /// <para></para>
        /// <para>LibVLC simple media player external API</para>
        /// </summary>
        /// <summary>Opaque equalizer handle.</summary>
        /// <remarks>Equalizer settings can be applied to a media player.</remarks>
        /// <summary>Callback prototype to allocate and lock a picture buffer.</summary>
        /// <param name="opaque">private pointer as passed to libvlc_video_set_callbacks() [IN]</param>
        /// <param name="planes">
        /// <para>start address of the pixel planes (LibVLC allocates the array</para>
        /// <para>of void pointers, this callback must initialize the array) [OUT]</para>
        /// </param>
        /// <returns>
        /// <para>a private pointer for the display and unlock callbacks to identify</para>
        /// <para>the picture buffers</para>
        /// </returns>
        /// <remarks>
        /// <para>Whenever a new video frame needs to be decoded, the lock callback is</para>
        /// <para>invoked. Depending on the video chroma, one or three pixel planes of</para>
        /// <para>adequate dimensions must be returned via the second parameter. Those</para>
        /// <para>planes must be aligned on 32-bytes boundaries.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr LibVLCVideoLockCb(IntPtr opaque, IntPtr planes);

        /// <summary>Callback prototype to unlock a picture buffer.</summary>
        /// <param name="opaque">private pointer as passed to libvlc_video_set_callbacks() [IN]</param>
        /// <param name="picture">private pointer returned from the</param>
        /// <param name="planes">pixel planes as defined by the</param>
        /// <remarks>
        /// <para>When the video frame decoding is complete, the unlock callback is invoked.</para>
        /// <para>This callback might not be needed at all. It is only an indication that the</para>
        /// <para>application can now read the pixel values if it needs to.</para>
        /// <para>A picture buffer is unlocked after the picture is decoded,</para>
        /// <para>but before the picture is displayed.</para>
        /// <para>callback [IN]</para>
        /// <para>callback (this parameter is only for convenience) [IN]</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCVideoUnlockCb(IntPtr opaque, IntPtr picture, IntPtr planes);

        /// <summary>Callback prototype to display a picture.</summary>
        /// <param name="opaque">private pointer as passed to libvlc_video_set_callbacks() [IN]</param>
        /// <param name="picture">private pointer returned from the</param>
        /// <remarks>
        /// <para>When the video frame needs to be shown, as determined by the media playback</para>
        /// <para>clock, the display callback is invoked.</para>
        /// <para>callback [IN]</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCVideoDisplayCb(IntPtr opaque, IntPtr picture);

        /// <summary>
        /// <para>Callback prototype to configure picture buffers format.</para>
        /// <para>This callback gets the format of the video as output by the video decoder</para>
        /// <para>and the chain of video filters (if any). It can opt to change any parameter</para>
        /// <para>as it needs. In that case, LibVLC will attempt to convert the video format</para>
        /// <para>(rescaling and chroma conversion) but these operations can be CPU intensive.</para>
        /// </summary>
        /// <param name="opaque">
        /// <para>pointer to the private pointer passed to</para>
        /// <para>libvlc_video_set_callbacks() [IN/OUT]</para>
        /// </param>
        /// <param name="chroma">pointer to the 4 bytes video format identifier [IN/OUT]</param>
        /// <param name="width">pointer to the pixel width [IN/OUT]</param>
        /// <param name="height">pointer to the pixel height [IN/OUT]</param>
        /// <param name="pitches">
        /// <para>table of scanline pitches in bytes for each pixel plane</para>
        /// <para>(the table is allocated by LibVLC) [OUT]</para>
        /// </param>
        /// <param name="lines">table of scanlines count for each plane [OUT]</param>
        /// <returns>the number of picture buffers allocated, 0 indicates failure</returns>
        /// <remarks>
        /// <para>For each pixels plane, the scanline pitch must be bigger than or equal to</para>
        /// <para>the number of bytes per pixel multiplied by the pixel width.</para>
        /// <para>Similarly, the number of scanlines must be bigger than of equal to</para>
        /// <para>the pixel height.</para>
        /// <para>Furthermore, we recommend that pitches and lines be multiple of 32</para>
        /// <para>to not break assumptions that might be held by optimized code</para>
        /// <para>in the video decoders, video filters and/or video converters.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint LibVLCVideoFormatCb(ref IntPtr opaque, IntPtr chroma, ref uint width,
            ref uint height, ref uint pitches, ref uint lines);

        /// <summary>Callback prototype to configure picture buffers format.</summary>
        /// <param name="opaque">
        /// <para>private pointer as passed to libvlc_video_set_callbacks()</para>
        /// <para>(and possibly modified by</para>
        /// </param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCVideoCleanupCb(ref IntPtr opaque);

        /// <summary>Callback prototype to setup the audio playback.</summary>
        /// <param name="opaque">
        /// <para>pointer to the data pointer passed to</para>
        /// <para>libvlc_audio_set_callbacks() [IN/OUT]</para>
        /// </param>
        /// <param name="format">4 bytes sample format [IN/OUT]</param>
        /// <param name="rate">sample rate [IN/OUT]</param>
        /// <param name="channels">channels count [IN/OUT]</param>
        /// <returns>0 on success, anything else to skip audio playback</returns>
        /// <remarks>This is called when the media player needs to create a new audio output.</remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int LibVLCAudioSetupCb(ref IntPtr opaque, ref IntPtr format, ref uint rate, ref uint channels);

        /// <summary>Callback prototype for audio playback cleanup.</summary>
        /// <param name="opaque">data pointer as passed to libvlc_audio_set_callbacks() [IN]</param>
        /// <remarks>This is called when the media player no longer needs an audio output.</remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCAudioCleanupCb(IntPtr opaque);

        /// <summary>Callback prototype for audio playback.</summary>
        /// <param name="data">data pointer as passed to libvlc_audio_set_callbacks() [IN]</param>
        /// <param name="samples">pointer to a table of audio samples to play back [IN]</param>
        /// <param name="count">number of audio samples to play back</param>
        /// <param name="pts">expected play time stamp (see libvlc_delay())</param>
        /// <remarks>
        /// <para>The LibVLC media player decodes and post-processes the audio signal</para>
        /// <para>asynchronously (in an internal thread). Whenever audio samples are ready</para>
        /// <para>to be queued to the output, this callback is invoked.</para>
        /// <para>The number of samples provided per invocation may depend on the file format,</para>
        /// <para>the audio coding algorithm, the decoder plug-in, the post-processing</para>
        /// <para>filters and timing. Application must not assume a certain number of samples.</para>
        /// <para>The exact format of audio samples is determined by libvlc_audio_set_format()</para>
        /// <para>or libvlc_audio_set_format_callbacks() as is the channels layout.</para>
        /// <para>Note that the number of samples is per channel. For instance, if the audio</para>
        /// <para>track sampling rate is 48000&#160;Hz, then 1200&#160;samples represent 25&#160;milliseconds</para>
        /// <para>of audio signal - regardless of the number of audio channels.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCAudioPlayCb(IntPtr data, IntPtr samples, uint count, long pts);

        /// <summary>Callback prototype for audio pause.</summary>
        /// <param name="data">data pointer as passed to libvlc_audio_set_callbacks() [IN]</param>
        /// <param name="pts">time stamp of the pause request (should be elapsed already)</param>
        /// <remarks>
        /// <para>LibVLC invokes this callback to pause audio playback.</para>
        /// <para>The pause callback is never called if the audio is already paused.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCAudioPauseCb(IntPtr data, long pts);

        /// <summary>Callback prototype for audio resumption.</summary>
        /// <param name="data">data pointer as passed to libvlc_audio_set_callbacks() [IN]</param>
        /// <param name="pts">time stamp of the resumption request (should be elapsed already)</param>
        /// <remarks>
        /// <para>LibVLC invokes this callback to resume audio playback after it was</para>
        /// <para>previously paused.</para>
        /// <para>The resume callback is never called if the audio is not paused.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCAudioResumeCb(IntPtr data, long pts);

        /// <summary>Callback prototype for audio buffer flush.
        /// <para>LibVLC invokes this callback if it needs to discard all pending buffers and</para>
        /// <para>stop playback as soon as possible. This typically occurs when the media is stopped.</para>
        /// </summary>
        /// <param name="data">data pointer as passed to libvlc_audio_set_callbacks() [IN]</param>
        /// <param name="pts">current presentation timestamp</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCAudioFlushCb(IntPtr data, long pts);

        /// <summary>Callback prototype for audio buffer drain.</summary>
        /// <param name="data">data pointer as passed to libvlc_audio_set_callbacks() [IN]</param>
        /// <remarks>
        /// <para>LibVLC may invoke this callback when the decoded audio track is ending.</para>
        /// <para>There will be no further decoded samples for the track, but playback should</para>
        /// <para>nevertheless continue until all already pending buffers are rendered.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCAudioDrainCb(IntPtr data);

        /// <summary>Callback prototype for audio volume change.</summary>
        /// <param name="data">data pointer as passed to libvlc_audio_set_callbacks() [IN]</param>
        /// <param name="volume">software volume (1. = nominal, 0. = mute)</param>
        /// <param name="mute">muted flag</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LibVLCVolumeCb(IntPtr data, float volume, [MarshalAs(UnmanagedType.I1)] bool mute);

        #endregion

        /// <summary>
        /// Get the Event Manager from which the media player send event.
        /// </summary>
        MediaPlayerEventManager EventManager
        {
            get
            {
                if (_eventManager == null)
                {
                    var eventManagerPtr = Native.LibVLCMediaPlayerEventManager(handle);
                    _eventManager = new MediaPlayerEventManager(eventManagerPtr);
                }
                return _eventManager;
            }
        }


        #region events

        /// <summary>
        /// The media of this mediaplayer changed
        /// </summary>
        public event EventHandler<MediaPlayerMediaChangedEventArgs> MediaChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerMediaChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerMediaChanged, value);
        }

        /// <summary>
        /// Nothing special to report
        /// </summary>
        public event EventHandler<EventArgs> NothingSpecial
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerNothingSpecial, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerNothingSpecial, value);
        }

        /// <summary>
        /// The mediaplayer is opening a media
        /// </summary>
        public event EventHandler<EventArgs> Opening
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerOpening, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerOpening, value);
        }

        /// <summary>
        /// The mediaplayer is buffering
        /// </summary>
        public event EventHandler<MediaPlayerBufferingEventArgs> Buffering
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerBuffering, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerBuffering, value);
        }

        /// <summary>
        /// The mediaplayer started playing a media
        /// </summary>
        public event EventHandler<EventArgs> Playing
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerPlaying, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerPlaying, value);
        }

        /// <summary>
        /// The mediaplayer paused playback
        /// </summary>
        public event EventHandler<EventArgs> Paused
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerPaused, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerPaused, value);
        }

        /// <summary>
        /// The mediaplayer stopped playback
        /// </summary>
        public event EventHandler<EventArgs> Stopped
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerStopped, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerStopped, value);
        }

        /// <summary>
        /// The mediaplayer went forward in the playback
        /// </summary>
        public event EventHandler<EventArgs> Forward
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerForward, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerForward, value);
        }

        /// <summary>
        /// The mediaplayer went backward in the playback
        /// </summary>
        public event EventHandler<EventArgs> Backward
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerBackward, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerBackward, value);
        }

        /// <summary>
        /// The mediaplayer reached the end of the playback
        /// </summary>
        public event EventHandler<EventArgs> EndReached
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerEndReached, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerEndReached, value);
        }

        /// <summary>
        /// The mediaplayer encountered an error during playback
        /// </summary>
        public event EventHandler<EventArgs> EncounteredError
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerEncounteredError, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerEncounteredError, value);
        }

        /// <summary>
        /// The mediaplayer's playback time changed
        /// </summary>
        public event EventHandler<MediaPlayerTimeChangedEventArgs> TimeChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerTimeChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerTimeChanged, value);
        }

        /// <summary>
        /// The mediaplayer's position changed
        /// </summary>
        public event EventHandler<MediaPlayerPositionChangedEventArgs> PositionChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerPositionChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerPositionChanged, value);
        }

        /// <summary>
        /// The mediaplayer's seek capability changed
        /// </summary>
        public event EventHandler<MediaPlayerSeekableChangedEventArgs> SeekableChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerSeekableChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerSeekableChanged, value);
        }

        /// <summary>
        /// The mediaplayer's pause capability changed
        /// </summary>
        public event EventHandler<MediaPlayerPausableChangedEventArgs> PausableChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerPausableChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerPausableChanged, value);
        }

        /// <summary>
        /// The title of the mediaplayer changed
        /// </summary>
        public event EventHandler<MediaPlayerTitleChangedEventArgs> TitleChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerTitleChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerTitleChanged, value);
        }

        /// <summary>
        /// The mediaplayer changed the chapter of a media
        /// </summary>
        public event EventHandler<MediaPlayerChapterChangedEventArgs> ChapterChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerChapterChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerChapterChanged, value);
        }

        /// <summary>
        /// The mediaplayer took a snapshot
        /// </summary>
        public event EventHandler<MediaPlayerSnapshotTakenEventArgs> SnapshotTaken
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerSnapshotTaken, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerSnapshotTaken, value);
        }

        /// <summary>
        /// The length of a playback changed
        /// </summary>
        public event EventHandler<MediaPlayerLengthChangedEventArgs> LengthChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerLengthChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerLengthChanged, value);
        }

        /// <summary>
        /// The Video Output count of the MediaPlayer changed
        /// </summary>
        public event EventHandler<MediaPlayerVoutEventArgs> Vout
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerVout, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerVout, value);
        }

        /// <summary>
        /// The mediaplayer scrambled status changed
        /// </summary>
        public event EventHandler<MediaPlayerScrambledChangedEventArgs> ScrambledChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerScrambledChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerScrambledChanged, value);
        }

        /// <summary>
        /// The mediaplayer has a new Elementary Stream (ES)
        /// </summary>
        public event EventHandler<MediaPlayerESAddedEventArgs> ESAdded
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerESAdded, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerESAdded, value);
        }

        /// <summary>
        /// The mediaplayer has one less Elementary Stream (ES)
        /// </summary>
        public event EventHandler<MediaPlayerESDeletedEventArgs> ESDeleted
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerESDeleted, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerESDeleted, value);
        }

        /// <summary>
        /// An Elementary Stream (ES) was selected
        /// </summary>
        public event EventHandler<MediaPlayerESSelectedEventArgs> ESSelected
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerESSelected, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerESSelected, value);
        }

        /// <summary>
        /// The mediaplayer's audio device changed
        /// </summary>
        public event EventHandler<MediaPlayerAudioDeviceEventArgs> AudioDevice
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerAudioDevice, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerAudioDevice, value);
        }

        /// <summary>
        /// The mediaplayer is corked
        /// </summary>
        public event EventHandler<EventArgs> Corked
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerCorked, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerCorked, value);
        }

        /// <summary>
        /// The mediaplayer is uncorked
        /// </summary>
        public event EventHandler<EventArgs> Uncorked
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerUncorked, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerUncorked, value);
        }

        /// <summary>
        /// The mediaplayer is muted
        /// </summary>
        public event EventHandler<EventArgs> Muted
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerMuted, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerMuted, value);
        }

        /// <summary>
        /// The mediaplayer is unmuted
        /// </summary>
        public event EventHandler<EventArgs> Unmuted
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerUnmuted, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerUnmuted, value);
        }

        /// <summary>
        /// The mediaplayer's volume changed
        /// </summary>
        public event EventHandler<MediaPlayerVolumeChangedEventArgs> VolumeChanged
        {
            add => EventManager.AttachEvent(EventType.MediaPlayerAudioVolume, value);
            remove => EventManager.DetachEvent(EventType.MediaPlayerAudioVolume, value);
        }

        #endregion

        /// <summary>
        /// Dispose override
        /// Effectively stops playback and disposes a media if any
        /// </summary>
        /// <param name="disposing">release any unmanaged resources</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_gcHandle.IsAllocated)
                    _gcHandle.Free();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>Description for titles</summary>
    public enum Title
    {
        /// <summary>
        /// Menu title
        /// </summary>
        Menu = 1,

        /// <summary>
        /// Interactive title
        /// </summary>
        Interactive = 2
    }

    /// <summary>Marq options definition</summary>
    public enum VideoMarqueeOption
    {
        /// <summary>
        /// Enable marquee
        /// </summary>
        Enable = 0,

        /// <summary>
        /// Text marquee
        /// </summary>
        Text = 1,

        /// <summary>
        /// Color marquee
        /// </summary>
        Color = 2,

        /// <summary>
        /// Opacity marquee
        /// </summary>
        Opacity = 3,

        /// <summary>
        /// Position marquee
        /// </summary>
        Position = 4,

        /// <summary>
        /// Refresh marquee
        /// </summary>
        Refresh = 5,

        /// <summary>
        /// Size marquee
        /// </summary>
        Size = 6,

        /// <summary>
        /// Timeout marquee
        /// </summary>
        Timeout = 7,

        /// <summary>
        /// X marquee
        /// </summary>
        X = 8,

        /// <summary>
        /// Y marquee
        /// </summary>
        Y = 9
    }

    /// <summary>Navigation mode</summary>
    public enum NavigationMode
    {
        /// <summary>
        /// Activate
        /// </summary>
        Activate = 0,

        /// <summary>
        /// Navigation up
        /// </summary>
        Up = 1,

        /// <summary>
        /// Navigation down
        /// </summary>
        Down = 2,

        /// <summary>
        /// Navigation left
        /// </summary>
        Left = 3,

        /// <summary>
        /// Navigation right
        /// </summary>
        Right = 4,

        /// <summary>
        /// Navigation popup
        /// </summary>
        Popup = 5
    }

    /// <summary>Enumeration of values used to set position (e.g. of video title).</summary>
    public enum Position
    {
        /// <summary>
        /// Disable
        /// </summary>
        Disable = -1,

        /// <summary>
        /// Center video title
        /// </summary>
        Center = 0,

        /// <summary>
        /// Left video title
        /// </summary>
        Left = 1,

        /// <summary>
        /// Right video title
        /// </summary>
        Right = 2,

        /// <summary>
        /// Top video title
        /// </summary>
        Top = 3,

        /// <summary>
        /// TopLeft video title
        /// </summary>
        TopLeft = 4,

        /// <summary>
        /// TopRight video title
        /// </summary>
        TopRight = 5,

        /// <summary>
        /// Bottom video title
        /// </summary>
        Bottom = 6,

        /// <summary>
        /// BottomLeft video title
        /// </summary>
        BottomLeft = 7,

        /// <summary>
        /// BottomRight video title
        /// </summary>
        BottomRight = 8
    }

    /// <summary>
    /// <para>Enumeration of teletext keys than can be passed via</para>
    /// <para>libvlc_video_set_teletext()</para>
    /// </summary>
    public enum TeletextKey
    {
        /// <summary>
        /// Red
        /// </summary>
        Red = 7471104,

        /// <summary>
        /// Green
        /// </summary>
        Green = 6750208,

        /// <summary>
        /// Yellow
        /// </summary>
        Yellow = 7929856,

        /// <summary>
        /// Blue
        /// </summary>
        Blue = 6422528,

        /// <summary>
        /// Index
        /// </summary>
        Index = 6881280
    }

    /// <summary>
    /// option values for libvlc_video_{get,set}_logo_{int,string}
    /// </summary>
    public enum VideoLogoOption
    {
        /// <summary>
        /// Enable
        /// </summary>
        Enable = 0,

        /// <summary>
        /// string argument, &quot;file,d,t;file,d,t;...&quot;
        /// </summary>
        File = 1,

        /// <summary>
        /// X
        /// </summary>
        X = 2,

        /// <summary>
        /// Y
        /// </summary>
        Y = 3,

        /// <summary>
        /// Delay
        /// </summary>
        Delay = 4,

        /// <summary>
        /// Repeat
        /// </summary>
        Repeat = 5,

        /// <summary>
        /// Opacity
        /// </summary>
        Opacity = 6,

        /// <summary>
        /// Position
        /// </summary>
        Position = 7
    }

    /// <summary>
    /// option values for libvlc_video_{get,set}_adjust_{int,float,bool}
    /// </summary>
    public enum VideoAdjustOption
    {
        /// <summary>
        /// Enable
        /// </summary>
        Enable = 0,

        /// <summary>
        /// Contrast
        /// </summary>
        Contrast = 1,

        /// <summary>
        /// Brightness
        /// </summary>
        Brightness = 2,

        /// <summary>
        /// Hue
        /// </summary>
        Hue = 3,

        /// <summary>
        /// Saturation
        /// </summary>
        Saturation = 4,

        /// <summary>
        /// Gamma
        /// </summary>
        Gamma = 5
    }

    /// <summary>
    /// Audio channels
    /// </summary>
    public enum AudioOutputChannel
    {
        /// <summary>
        /// Error
        /// </summary>
        Error = -1,

        /// <summary>
        /// Stereo mode
        /// </summary>
        Stereo = 1,

        /// <summary>
        /// RStereo mode
        /// </summary>
        RStereo = 2,

        /// <summary>
        /// Left mode
        /// </summary>
        Left = 3,

        /// <summary>
        /// Right mode
        /// </summary>
        Right = 4,

        /// <summary>
        /// Dolbys mode
        /// </summary>
        Dolbys = 5
    }

    /// <summary>Media player roles.</summary>
    /// <remarks>
    /// <para>LibVLC 3.0.0 and later.</para>
    /// <para>See</para>
    /// </remarks>
    public enum MediaPlayerRole
    {
        /// <summary>Don't use a media player role</summary>
        None = 0,
        /// <summary>Music (or radio) playback</summary>
        Music = 1,
        /// <summary>Video playback</summary>
        Video = 2,
        /// <summary>Speech, real-time communication</summary>
        Communication = 3,
        /// <summary>Video game</summary>
        Game = 4,
        /// <summary>User interaction feedback</summary>
        LiblvcRoleNotification = 5,
        /// <summary>Embedded animation (e.g. in web page)</summary>
        Animation = 6,
        /// <summary>Audio editing/production</summary>
        Production = 7,
        /// <summary>Accessibility</summary>
        Accessibility = 8,
        /// <summary>Testing</summary>
        Test = 9
    }
}
