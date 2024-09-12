using LibVLCSharp.Shared.Helpers;
using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// Equalizer settings can be applied to a media player using this type
    /// </summary>
    public partial class Equalizer : Internal
    {
        struct Native
        {
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_new")]
            internal static extern IntPtr LibVLCAudioEqualizerNew();

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_release")]
            internal static extern void LibVLCAudioEqualizerRelease(IntPtr equalizer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_new_from_preset")]
            internal static extern IntPtr LibVLCAudioEqualizerNewFromPreset(uint index);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_set_preamp")]
            internal static extern int LibVLCAudioEqualizerSetPreamp(IntPtr equalizer, float preamp);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_get_preamp")]
            internal static extern float LibVLCAudioEqualizerGetPreamp(IntPtr equalizer);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_set_amp_at_index")]
            internal static extern int LibVLCAudioEqualizerSetAmpAtIndex(IntPtr equalizer, float amp, uint band);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_get_amp_at_index")]
            internal static extern float LibVLCAudioEqualizerGetAmpAtIndex(IntPtr equalizer, uint band);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_get_preset_count")]
            internal static extern uint LibVLCAudioEqualizerGetPresetCount();

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_get_preset_name")]
            internal static extern IntPtr LibVLCAudioEqualizerGetPresetName(uint index);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_get_band_count")]
            internal static extern uint LibVLCAudioEqualizerGetBandCount();

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_audio_equalizer_get_band_frequency")]
            internal static extern float LibVLCAudioEqualizerGetBandFrequency(uint index);
        }

        /// <summary>
        /// Create a new default equalizer, with all frequency values zeroed.
        /// The new equalizer can subsequently be applied to a media player by invoking
        /// libvlc_media_player_set_equalizer().
        /// version LibVLC 2.2.0 or later
        /// </summary>
        public Equalizer()
            : base(Native.LibVLCAudioEqualizerNew())
        {
        }

        /// <summary>
        /// Create a new equalizer, with initial frequency values copied from an existing preset.
        /// The new equalizer can subsequently be applied to a media player by invoking
        /// libvlc_media_player_set_equalizer().
        /// version LibVLC 2.2.0 or later
        /// </summary>
        /// <param name="index">index of the preset, counting from zero</param>
        public Equalizer(uint index)
            : base(Native.LibVLCAudioEqualizerNewFromPreset(index))
        {
        }

        protected override bool ReleaseHandle()
        {
            Native.LibVLCAudioEqualizerRelease(handle);
            return true;
        }

        /// <summary>
        /// Set a new pre-amplification value for an equalizer.
        /// The new equalizer settings are subsequently applied to a media player by invoking
        /// MediaPlayer::setEqualizer().
        /// The supplied amplification value will be clamped to the -20.0 to +20.0 range.
        /// </summary>
        /// <param name="preamp">preamp value (-20.0 to 20.0 Hz)</param>
        ///  LibVLC 2.2.0 or later
        /// <returns>true on success, false otherwise</returns>
        public bool SetPreamp(float preamp) => Native.LibVLCAudioEqualizerSetPreamp(handle, preamp) == 0;

        /// <summary>
        /// Get the current pre-amplification value from an equalizer.
        /// return preamp value (Hz)
        /// LibVLC 2.2.0 or later
        /// </summary>
        public float Preamp => Native.LibVLCAudioEqualizerGetPreamp(handle);

        /// <summary>
        /// Set a new amplification value for a particular equalizer frequency band.
        /// The new equalizer settings are subsequently applied to a media player by invoking MediaPlayer::setEqualizer().
        /// The supplied amplification value will be clamped to the -20.0 to +20.0 range.
        /// LibVLC 2.2.0 or later
        /// </summary>
        /// <param name="amp">amplification value (-20.0 to 20.0 Hz)</param>
        /// <param name="band">index, counting from zero, of the frequency band to set</param>
        public bool SetAmp(float amp, uint band) =>
            Native.LibVLCAudioEqualizerSetAmpAtIndex(handle, amp, band) == 0;

        /// <summary>
        /// Get the amplification value for a particular equalizer frequency band.
        /// LibVLC 2.2.0 or later
        /// </summary>
        /// <param name="band">index, counting from zero, of the frequency band to get</param>
        /// <returns>amplification value (Hz); NaN if there is no such frequency band</returns>
        public float Amp(uint band) => Native.LibVLCAudioEqualizerGetAmpAtIndex(handle, band);

        /// <summary>
        /// Get the number of equalizer presets.
        /// LibVLC 2.2.0 or later
        /// </summary>
        public uint PresetCount => Native.LibVLCAudioEqualizerGetPresetCount();

        /// <summary>
        /// Get the name of a particular equalizer preset.
        /// This name can be used, for example, to prepare a preset label or menu in a user interface.
        /// </summary>
        /// <param name="index">index of the preset, counting from zero</param>
        /// <returns>preset name, or empty string if there is no such preset</returns>
        public string PresetName(uint index) => Native.LibVLCAudioEqualizerGetPresetName(index).FromUtf8();

        /// <summary>
        /// Get the number of distinct frequency bands for an equalizer.
        /// return number of frequency bands
        /// LibVLC 2.2.0 or later
        /// </summary>
        public uint BandCount => Native.LibVLCAudioEqualizerGetBandCount();

        /// <summary>
        /// Get a particular equalizer band frequency.
        /// This value can be used, for example, to create a label for an equalizer band control in a user interface.
        /// LibVLC 2.2.0 or later
        /// </summary>
        /// <param name="index">index index of the band, counting from zero</param>
        /// <returns>equalizer band frequency (Hz), or -1 if there is no such band</returns>
        public float BandFrequency(uint index) => Native.LibVLCAudioEqualizerGetBandFrequency(index);
    }
}
