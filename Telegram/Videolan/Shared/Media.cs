using LibVLCSharp.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// Media is an abstract representation of a playable media. It can be a network stream or a local video/audio file.
    /// </summary>
    public partial class Media : Internal
    {
        internal struct Native
        {
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_new_location")]
            internal static extern IntPtr LibVLCMediaNewLocation(LibVLC libVLC, IntPtr mrl);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_new_path")]
            internal static extern IntPtr LibVLCMediaNewPath(LibVLC libVLC, IntPtr path);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_new_as_node")]
            internal static extern IntPtr LibVLCMediaNewAsNode(LibVLC libVLC, IntPtr name);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_new_fd")]
            internal static extern IntPtr LibVLCMediaNewFd(LibVLC libVLC, int fd);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_release")]
            internal static extern void LibVLCMediaRelease(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_media")]
            internal static extern IntPtr LibVLCMediaListMedia(MediaList mediaList);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_new_callbacks")]
            internal static extern IntPtr LibVLCMediaNewCallbacks(LibVLC libVLC, InternalOpenMedia openCb, InternalReadMedia readCb,
                InternalSeekMedia seekCb, InternalCloseMedia closeCb, IntPtr opaque);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_add_option")]
            internal static extern void LibVLCMediaAddOption(IntPtr media, IntPtr option);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_add_option_flag")]
            internal static extern void LibVLCMediaAddOptionFlag(IntPtr media, IntPtr options, uint flags);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_mrl")]
            internal static extern IntPtr LibVLCMediaGetMrl(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_duplicate")]
            internal static extern IntPtr LibVLCMediaDuplicate(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_meta")]
            internal static extern IntPtr LibVLCMediaGetMeta(IntPtr media, MetadataType metadataType);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_set_meta")]
            internal static extern void LibVLCMediaSetMeta(IntPtr media, MetadataType metadataType, IntPtr value);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_save_meta")]
            internal static extern int LibVLCMediaSaveMeta(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_state")]
            internal static extern VLCState LibVLCMediaGetState(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_event_manager")]
            internal static extern IntPtr LibVLCMediaEventManager(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_stats")]
            internal static extern int LibVLCMediaGetStats(IntPtr media, out MediaStats statistics);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_duration")]
            internal static extern long LibVLCMediaGetDuration(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_is_parsed")]
            internal static extern int LibVLCMediaIsParsed(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_parse_with_options")]
            internal static extern int LibVLCMediaParseWithOptions(IntPtr media, MediaParseOptions mediaParseOptions, int timeout);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_parsed_status")]
            internal static extern MediaParsedStatus LibVLCMediaGetParsedStatus(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_parse_stop")]
            internal static extern void LibVLCMediaParseStop(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_set_user_data")]
            internal static extern void LibVLCMediaSetUserData(IntPtr media, IntPtr userData);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_user_data")]
            internal static extern IntPtr LibVLCMediaGetUserData(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_tracks_get")]
            internal static extern uint LibVLCMediaTracksGet(IntPtr media, out IntPtr tracksPtr);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_tracks_release")]
            internal static extern void LibVLCMediaTracksRelease(IntPtr tracks, uint count);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_subitems")]
            internal static extern IntPtr LibVLCMediaSubitems(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_type")]
            internal static extern MediaType LibVLCMediaGetType(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_slaves_add")]
            internal static extern int LibVLCMediaAddSlaves(IntPtr media, MediaSlaveType slaveType, uint priority, IntPtr uri);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_slaves_clear")]
            internal static extern void LibVLCMediaClearSlaves(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_slaves_get")]
            internal static extern uint LibVLCMediaGetSlaves(IntPtr media, out IntPtr slaves);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_slaves_release")]
            internal static extern void LibVLCMediaReleaseSlaves(IntPtr slaves, uint count);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_retain")]
            internal static extern void LibVLCMediaRetain(IntPtr media);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_get_codec_description")]
            internal static extern IntPtr LibvlcMediaGetCodecDescription(TrackType type, uint codec);
        }

        Media(IntPtr create, params string[] options)
            : base(create)
        {
            if (options == null) return;

            foreach (var optionUtf8 in options.ToUtf8())
                if (optionUtf8 != IntPtr.Zero)
                    MarshalUtils.PerformInteropAndFree(() => Native.LibVLCMediaAddOption(handle, optionUtf8), optionUtf8);
        }

        /// <summary>
        /// Media Constructs a libvlc Media instance
        /// </summary>
        /// <param name="libVLC">A libvlc instance</param>
        /// <param name="mrl">A path, location, or node name, depending on the 3rd parameter</param>
        /// <param name="type">The type of the 2nd argument.</param>
        /// <param name="options">the libvlc options, in the form of ":your-option"</param>
        public Media(LibVLC libVLC, string mrl, FromType type = FromType.FromPath, params string[] options)
            : this(SelectNativeCtor(libVLC, mrl, type), options)
        {
        }

        /// <summary>
        /// Media Constructs a libvlc Media instance
        /// </summary>
        /// <param name="libVLC">A libvlc instance</param>
        /// <param name="uri">The absolute URI of the resource.</param>
        /// <param name="options">the libvlc options, in the form of ":your-option"</param>
        public Media(LibVLC libVLC, Uri uri, params string[] options)
            : this(SelectNativeCtor(libVLC, uri?.AbsoluteUri ?? string.Empty, FromType.FromLocation),
                  options)
        {
        }

        /// <summary>
        /// Create a media for an already open file descriptor.
        /// The file descriptor shall be open for reading(or reading and writing).
        ///
        /// Regular file descriptors, pipe read descriptors and character device
        /// descriptors(including TTYs) are supported on all platforms.
        /// Block device descriptors are supported where available.
        /// Directory descriptors are supported on systems that provide fdopendir().
        /// Sockets are supported on all platforms where they are file descriptors,
        /// i.e.all except Windows.
        ///
        /// \note This library will <b>not</b> automatically close the file descriptor
        /// under any circumstance.Nevertheless, a file descriptor can usually only be
        /// rendered once in a media player.To render it a second time, the file
        /// descriptor should probably be rewound to the beginning with lseek().
        /// </summary>
        /// <param name="libVLC">A libvlc instance</param>
        /// <param name="fd">open file descriptor</param>
        /// <param name="options">the libvlc options, in the form of ":your-option"</param>
        public Media(LibVLC libVLC, int fd, params string[] options)
            : this(Native.LibVLCMediaNewFd(libVLC, fd), options)
        {
        }

        /// <summary>
        /// Create a media from a media list
        /// </summary>
        /// <param name="mediaList">media list to create media from</param>
        public Media(MediaList mediaList)
            : base(Native.LibVLCMediaListMedia(mediaList))
        {
        }

        /// <summary>
        /// Create a media from a MediaInput
        /// requires libvlc 3.0 or higher
        /// </summary>
        /// <param name="libVLC">the libvlc instance</param>
        /// <param name="input">the media to be used by libvlc. LibVLCSharp will NOT dispose or close it.
        /// Use <see cref="StreamMediaInput"/> or implement your own.</param>
        /// <param name="options">the libvlc options, in the form of ":your-option"</param>
        public Media(LibVLC libVLC, MediaInput input, params string[] options)
            : this(CtorFromInput(libVLC, input), options)
        {
        }

        internal Media(IntPtr mediaPtr)
            : base(mediaPtr)
        {
        }

        protected override bool ReleaseHandle()
        {
            Native.LibVLCMediaRelease(handle);
            return true;
        }

        static IntPtr SelectNativeCtor(LibVLC libVLC, string mrl, FromType type)
        {
            if (libVLC == null)
                throw new ArgumentNullException(nameof(libVLC));
            if (string.IsNullOrEmpty(mrl))
                throw new ArgumentNullException(nameof(mrl));

            if (PlatformHelper.IsWindows && type == FromType.FromPath)
            {
                mrl = mrl.Replace("/", @"\");
            }

            var mrlPtr = mrl.ToUtf8();
            if (mrlPtr == IntPtr.Zero)
                throw new ArgumentException($"error marshalling {mrl} to UTF-8 for native interop");

            IntPtr result;
            switch (type)
            {
                case FromType.FromLocation:
                    result = Native.LibVLCMediaNewLocation(libVLC, mrlPtr);
                    break;
                case FromType.FromPath:
                    result = Native.LibVLCMediaNewPath(libVLC, mrlPtr);
                    break;
                case FromType.AsNode:
                    result = Native.LibVLCMediaNewAsNode(libVLC, mrlPtr);
                    break;
                default:
                    result = IntPtr.Zero;
                    break;
            }

            Marshal.FreeHGlobal(mrlPtr);

            return result;
        }

        static IntPtr CtorFromInput(LibVLC libVLC, MediaInput input)
        {
            if (libVLC == null)
                throw new ArgumentNullException(nameof(libVLC));
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            return Native.LibVLCMediaNewCallbacks(libVLC,
                OpenMediaCallbackHandle,
                ReadMediaCallbackHandle,
                input.CanSeek ? SeekMediaCallbackHandle : null,
                CloseMediaCallbackHandle,
                GCHandle.ToIntPtr(input.GcHandle));
        }

        /// <summary>Add an option to the media.
        /// <example>
        /// <code>
        /// // example <br/>
        /// media.AddOption(":no-audio");
        /// </code>
        /// </example></summary>
        /// <param name="option">the media option, in the form of ":your-option"</param>
        /// <remarks>
        /// <para>This option will be used to determine how the media_player will</para>
        /// <para>read the media. This allows to use VLC's advanced</para>
        /// <para>reading/streaming options on a per-media basis.</para>
        /// <para>The options are listed in 'vlc --long-help' from the command line,</para>
        /// <para>e.g. &quot;-sout-all&quot;. Keep in mind that available options and their semantics</para>
        /// <para>vary across LibVLC versions and builds.</para>
        /// <para>Not all options affects libvlc_media_t objects:</para>
        /// <para>Specifically, due to architectural issues most audio and video options,</para>
        /// <para>such as text renderer options, have no effects on an individual media.</para>
        /// <para>These options must be set through libvlc_new() instead.</para>
        /// </remarks>
        public void AddOption(string option)
        {
            if (string.IsNullOrEmpty(option)) throw new ArgumentNullException(nameof(option));

            var optionUtf8 = option.ToUtf8();
            MarshalUtils.PerformInteropAndFree(() => Native.LibVLCMediaAddOption(handle, optionUtf8), optionUtf8);
        }

        /// <summary>
        /// Convenience method for crossplatform media configuration
        /// </summary>
        /// <param name="mediaConfiguration">mediaConfiguration translate to strings parsed by the vlc engine, some are platform specific</param>
        public void AddOption(MediaConfiguration mediaConfiguration)
        {
            if (mediaConfiguration == null) throw new ArgumentNullException(nameof(mediaConfiguration));

            foreach (var option in mediaConfiguration.Build())
            {
                AddOption(option);
            }
        }

        /// <summary>Add an option to the media with configurable flags.</summary>
        /// <param name="option">the media option</param>
        /// <param name="flags">the flags for this option</param>
        /// <remarks>
        /// <para>This option will be used to determine how the media_player will</para>
        /// <para>read the media. This allows to use VLC's advanced</para>
        /// <para>reading/streaming options on a per-media basis.</para>
        /// <para>The options are detailed in vlc --long-help, for instance</para>
        /// <para>&quot;--sout-all&quot;. Note that all options are not usable on medias:</para>
        /// <para>specifically, due to architectural issues, video-related options</para>
        /// <para>such as text renderer options cannot be set on a single media. They</para>
        /// <para>must be set on the whole libvlc instance instead.</para>
        /// </remarks>
        public void AddOptionFlag(string option, uint flags)
        {
            if (string.IsNullOrEmpty(option)) throw new ArgumentNullException(nameof(option));

            var optionUtf8 = option.ToUtf8();

            MarshalUtils.PerformInteropAndFree(() => Native.LibVLCMediaAddOptionFlag(handle, optionUtf8, flags), optionUtf8);
        }

        string _mrl;
        /// <summary>Get the media resource locator (mrl) from a media descriptor object</summary>
        public string Mrl
        {
            get
            {
                if (string.IsNullOrEmpty(_mrl))
                {
                    var mrlPtr = Native.LibVLCMediaGetMrl(handle);
                    _mrl = mrlPtr.FromUtf8(libvlcFree: true);
                }
                return _mrl!;
            }
        }

        /// <summary>Duplicate a media descriptor object.</summary>
        public Media Duplicate()
        {
            var duplicatePtr = Native.LibVLCMediaDuplicate(handle);
            if (duplicatePtr == IntPtr.Zero) throw new Exception("Failure to duplicate");
            return new Media(duplicatePtr);
        }

        /// <summary>Read the meta of the media.</summary>
        /// <param name="metadataType">the meta to read</param>
        /// <returns>the media's meta</returns>
        /// <remarks>
        /// If the media has not yet been parsed this will return NULL.
        /// </remarks>
        public string Meta(MetadataType metadataType)
        {
            var metaPtr = Native.LibVLCMediaGetMeta(handle, metadataType);
            return metaPtr.FromUtf8(libvlcFree: true);
        }

        /// <summary>
        /// <para>Set the meta of the media (this function will not save the meta, call</para>
        /// <para>libvlc_media_save_meta in order to save the meta)</para>
        /// </summary>
        /// <param name="metadataType">the <see cref="MetadataType"/>  to write</param>
        /// <param name="metaValue">the media's meta</param>
        public void SetMeta(MetadataType metadataType, string metaValue)
        {
            if (string.IsNullOrEmpty(metaValue)) throw new ArgumentNullException(metaValue);

            var metaUtf8 = metaValue.ToUtf8();
            MarshalUtils.PerformInteropAndFree(() => Native.LibVLCMediaSetMeta(handle, metadataType, metaUtf8), metaUtf8);
        }

        /// <summary>Save the meta previously set</summary>
        /// <returns>true if the write operation was successful</returns>
        public bool SaveMeta() => Native.LibVLCMediaSaveMeta(handle) != 0;

        /// <summary>
        /// Get current <see cref="VLCState"/> of media descriptor object.
        /// </summary>
        public VLCState State => Native.LibVLCMediaGetState(handle);

        /// <summary>Get the current statistics about the media
        /// structure that contain the statistics about the media
        /// </summary>
        public MediaStats Statistics => Native.LibVLCMediaGetStats(handle, out var mediaStats) == 0
            ? default : mediaStats;

        MediaEventManager _eventManager;
        /// <summary>
        /// <para>Get event manager from media descriptor object.</para>
        /// <para>NOTE: this function doesn't increment reference counting.</para>
        /// </summary>
        /// <returns>event manager object</returns>
        MediaEventManager EventManager
        {
            get
            {
                if (_eventManager != null) return _eventManager;
                var eventManagerPtr = Native.LibVLCMediaEventManager(handle);
                _eventManager = new MediaEventManager(eventManagerPtr);
                return _eventManager;
            }
        }

        /// <summary>Get duration (in ms) of media descriptor object item.</summary>
        /// <returns>duration of media item or -1 on error</returns>
        public long Duration => Native.LibVLCMediaGetDuration(handle);

        /// <summary>
        /// Parse the media asynchronously with options.      
        /// It uses a flag to specify parse options (see <see cref="MediaParseOptions"/>). All these flags can be combined. By default, the media is parsed only if it's a local file.
        /// <para/> Note: Parsing can be aborted with ParseStop().
        /// </summary>
        /// <param name="options">Parse options flags. They can be combined</param>
        /// <param name="timeout">maximum time allowed to preparse the media. 
        /// <para/>If -1, the default "preparse-timeout" option will be used as a timeout. 
        /// <para/>If 0, it will wait indefinitely. If > 0, the timeout will be used (in milliseconds). 
        /// </param>
        /// <param name="cancellationToken">token to cancel the operation</param>
        /// <returns>the parse status of the media</returns>
        public async Task<MediaParsedStatus> Parse(MediaParseOptions options = MediaParseOptions.ParseLocal, int timeout = -1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<MediaParsedStatus>();
            var cancellationTokenRegistration = cancellationToken.Register(() =>
            {
                ParsedChanged -= OnParsedChanged;
                Native.LibVLCMediaParseStop(handle);
                tcs.TrySetCanceled();
            });

            void OnParsedChanged(object sender, MediaParsedChangedEventArgs mediaParsedChangedEventArgs)
                => tcs.TrySetResult(mediaParsedChangedEventArgs.ParsedStatus);

            try
            {
                ParsedChanged += OnParsedChanged;

                var result = Native.LibVLCMediaParseWithOptions(handle, options, timeout);
                if (result == -1)
                {
                    tcs.TrySetResult(MediaParsedStatus.Failed);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                cancellationTokenRegistration.Dispose();
                ParsedChanged -= OnParsedChanged;
            }
        }

        /// <summary>Return true is the media descriptor object is parsed</summary>
        /// <returns>true if media object has been parsed otherwise it returns false</returns>
        public bool IsParsed => Native.LibVLCMediaIsParsed(handle) != 0;

        /// <summary>Get Parsed status for media descriptor object.</summary>
        /// <returns>a value of the libvlc_media_parsed_status_t enum</returns>
        /// <remarks>
        /// <para>libvlc_MediaParsedChanged</para>
        /// <para>libvlc_media_parsed_status_t</para>
        /// <para>LibVLC 3.0.0 or later</para>
        /// </remarks>
        public MediaParsedStatus ParsedStatus => Native.LibVLCMediaGetParsedStatus(handle);

        /// <summary>Stop the parsing of the media</summary>
        /// <remarks>
        /// <para>When the media parsing is stopped, the libvlc_MediaParsedChanged event will</para>
        /// <para>be sent with the libvlc_media_parsed_status_timeout status.</para>
        /// <para>libvlc_media_parse_with_options</para>
        /// <para>LibVLC 3.0.0 or later</para>
        /// </remarks>
        public void ParseStop() => Native.LibVLCMediaParseStop(handle);

        /// <summary>Get media descriptor's elementary streams description
        /// <para>address to store an allocated array of Elementary Streams</para>
        /// <para>descriptions (must be freed with libvlc_media_tracks_release</para>
        /// <para>by the caller) [OUT]</para>
        /// <returns>the number of Elementary Streams (zero on error)</returns>
        /// <remarks>
        /// <para>Note, you need to call libvlc_media_parse() or play the media at least once</para>
        /// <para>before calling this function.</para>
        /// <para>Not doing this will result in an empty array.</para>
        /// <para>LibVLC 2.1.0 and later.</para>
        /// </remarks>
        /// </summary>
        public MediaTrack[] Tracks => MarshalUtils.Retrieve(handle, (IntPtr nativeRef, out IntPtr array) => Native.LibVLCMediaTracksGet(nativeRef, out array),
            MarshalUtils.PtrToStructure<MediaTrackStructure>,
            m => m.Build(),
            Native.LibVLCMediaTracksRelease);

        /// <summary>
        /// <para>Get subitems of media descriptor object. This will increment</para>
        /// <para>the reference count of supplied media descriptor object. Use</para>
        /// <para>libvlc_media_list_release() to decrement the reference counting.</para>
        /// </summary>
        /// <returns>list of media descriptor subitems or NULL</returns>
        public MediaList SubItems => new MediaList(Native.LibVLCMediaSubitems(handle));

        /// <summary>
        /// The type of the media
        /// </summary>
        public MediaType Type => Native.LibVLCMediaGetType(handle);

        /// <summary>Add a slave to the current media.</summary>
        /// <param name="type">subtitle or audio</param>
        /// <param name="priority">from 0 (low priority) to 4 (high priority)</param>
        /// <param name="uri">Uri of the slave (should contain a valid scheme).</param>
        /// <returns>true on success, false on error.</returns>
        /// <remarks>
        /// <para>A slave is an external input source that may contains an additional subtitle</para>
        /// <para>track (like a .srt) or an additional audio track (like a .ac3).</para>
        /// <para>This function must be called before the media is parsed (via</para>
        /// <para>libvlc_media_parse_with_options()) or before the media is played (via</para>
        /// <para>libvlc_media_player_play())</para>
        /// <para>LibVLC 3.0.0 and later.</para>
        /// </remarks>
        public bool AddSlave(MediaSlaveType type, uint priority, string uri)
        {
            var uriUtf8 = uri.ToUtf8();
            return MarshalUtils.PerformInteropAndFree(() => Native.LibVLCMediaAddSlaves(handle, type, priority, uriUtf8) == 0, uriUtf8);
        }

        /// <summary>Add a slave to the current media.</summary>
        /// <param name="type">subtitle or audio</param>
        /// <param name="priority">from 0 (low priority) to 4 (high priority)</param>
        /// <param name="uri">Uri of the slave (should contain a valid scheme).</param>
        /// <returns>true on success, false on error.</returns>
        /// <remarks>
        /// <para>A slave is an external input source that may contains an additional subtitle</para>
        /// <para>track (like a .srt) or an additional audio track (like a .ac3).</para>
        /// <para>This function must be called before the media is parsed (via</para>
        /// <para>libvlc_media_parse_with_options()) or before the media is played (via</para>
        /// <para>libvlc_media_player_play())</para>
        /// <para>LibVLC 3.0.0 and later.</para>
        /// </remarks>
        public bool AddSlave(MediaSlaveType type, uint priority, Uri uri)
        {
            var uriUtf8 = uri?.AbsoluteUri?.ToUtf8() ?? IntPtr.Zero;
            return MarshalUtils.PerformInteropAndFree(() => Native.LibVLCMediaAddSlaves(handle, type, priority, uriUtf8) == 0, uriUtf8);
        }

        /// <summary>
        /// <para>Clear all slaves previously added by libvlc_media_slaves_add() or</para>
        /// <para>internally.</para>
        /// </summary>
        /// <remarks>LibVLC 3.0.0 and later.</remarks>
        public void ClearSlaves() => Native.LibVLCMediaClearSlaves(handle);

        /// <summary>Get a media descriptor's slave list</summary>
        /// <para>address to store an allocated array of slaves (must be</para>
        /// <para>freed with libvlc_media_slaves_release()) [OUT]</para>
        /// <returns>the number of slaves (zero on error)</returns>
        /// <remarks>
        /// <para>The list will contain slaves parsed by VLC or previously added by</para>
        /// <para>libvlc_media_slaves_add(). The typical use case of this function is to save</para>
        /// <para>a list of slave in a database for a later use.</para>
        /// <para>LibVLC 3.0.0 and later.</para>
        /// <para>libvlc_media_slaves_add</para>
        /// </remarks>
        public MediaSlave[] Slaves => MarshalUtils.Retrieve(handle, (IntPtr nativeRef, out IntPtr array) => Native.LibVLCMediaGetSlaves(nativeRef, out array),
            MarshalUtils.PtrToStructure<MediaSlaveStructure>,
            s => s.Build(),
            Native.LibVLCMediaReleaseSlaves);

        /// <summary>Get a media's codec description</summary>
        /// <param name="type">The type of the track</param>
        /// <param name="codec">the codec or fourcc</param>
        /// <returns>the codec description</returns>
        public string CodecDescription(TrackType type, uint codec) => Native.LibvlcMediaGetCodecDescription(type, codec).FromUtf8()!;

        /// <summary>
        /// Equality override for this media instance
        /// </summary>
        /// <param name="obj">the media to compare this one with</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is Media media &&
                   EqualityComparer<IntPtr>.Default.Equals(handle, media.handle);
        }

        /// <summary>
        /// Custom hascode implemenation for this Media instance
        /// </summary>
        /// <returns>the hashcode for this Media instance</returns>
        public override int GetHashCode()
        {
            return handle.GetHashCode();
        }

        /// <summary>Increments the native reference counter for the media</summary>
        internal void Retain()
        {
            if (!IsInvalid)
                Native.LibVLCMediaRetain(handle);
        }

        internal override void OnNativeInstanciationError()
        {
            throw new VLCException("Failed to instantiate the Media on the native side. " +
                    $"{Environment.NewLine}Have you installed the latest LibVLC package from nuget for your target platform?" +
                    $"{Environment.NewLine}Is your MRL correct Do check the native LibVLC verbose logs for more information.");
        }

        #region MediaFromStream

        static readonly InternalOpenMedia OpenMediaCallbackHandle = OpenMediaCallback;
        static readonly InternalReadMedia ReadMediaCallbackHandle = ReadMediaCallback;
        static readonly InternalSeekMedia SeekMediaCallbackHandle = SeekMediaCallback;
        static readonly InternalCloseMedia CloseMediaCallbackHandle = CloseMediaCallback;

        [MonoPInvokeCallback(typeof(InternalOpenMedia))]
        static int OpenMediaCallback(IntPtr opaque, ref IntPtr data, out ulong size)
        {
            data = opaque;
            var input = MarshalUtils.GetInstance<MediaInput>(opaque);
            if (input == null)
            {
                size = 0UL;
                return -1;
            }

            return input.Open(out size) ? 0 : -1;
        }

        [MonoPInvokeCallback(typeof(InternalReadMedia))]
        static int ReadMediaCallback(IntPtr opaque, IntPtr buf, uint len)
        {
            var input = MarshalUtils.GetInstance<MediaInput>(opaque);
            if (input == null)
            {
                return -1;
            }
            return input.Read(buf, len);
        }

        [MonoPInvokeCallback(typeof(InternalSeekMedia))]
        static int SeekMediaCallback(IntPtr opaque, ulong offset)
        {
            var input = MarshalUtils.GetInstance<MediaInput>(opaque);
            if (input == null)
            {
                return -1;
            }
            return input.Seek(offset) ? 0 : -1;
        }

        [MonoPInvokeCallback(typeof(InternalCloseMedia))]
        static void CloseMediaCallback(IntPtr opaque)
        {
            var input = MarshalUtils.GetInstance<MediaInput>(opaque);
            input?.Close();
        }

        #endregion

        #region MediaFromCallbacks

        /// <summary>
        /// <para>It consists of a media location and various optional meta data.</para>
        /// <para>@{</para>
        /// <para></para>
        /// <para>LibVLC media item/descriptor external API</para>
        /// </summary>
        /// <summary>Callback prototype to open a custom bitstream input media.</summary>
        /// <param name="opaque">private pointer as passed to libvlc_media_new_callbacks()</param>
        /// <param name="data">storage space for a private data pointer [OUT]</param>
        /// <param name="size">byte length of the bitstream or UINT64_MAX if unknown [OUT]</param>
        /// <returns>
        /// <para>0 on success, non-zero on error. In case of failure, the other</para>
        /// <para>callbacks will not be invoked and any value stored in *datap and *sizep is</para>
        /// <para>discarded.</para>
        /// </returns>
        /// <remarks>
        /// <para>The same media item can be opened multiple times. Each time, this callback</para>
        /// <para>is invoked. It should allocate and initialize any instance-specific</para>
        /// <para>resources, then store them in *datap. The instance resources can be freed</para>
        /// <para>in the</para>
        /// <para>For convenience, *datap is initially NULL and *sizep is initially 0.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int InternalOpenMedia(IntPtr opaque, ref IntPtr data, out ulong size);

        /// <summary>Callback prototype to read data from a custom bitstream input media.</summary>
        /// <param name="opaque">private pointer as set by the</param>
        /// <param name="buf">start address of the buffer to read data into</param>
        /// <param name="len">bytes length of the buffer</param>
        /// <returns>
        /// <para>strictly positive number of bytes read, 0 on end-of-stream,</para>
        /// <para>or -1 on non-recoverable error</para>
        /// </returns>
        /// <remarks>
        /// <para>callback</para>
        /// <para>If no data is immediately available, then the callback should sleep.</para>
        /// <para>The application is responsible for avoiding deadlock situations.</para>
        /// <para>In particular, the callback should return an error if playback is stopped;</para>
        /// <para>if it does not return, then libvlc_media_player_stop() will never return.</para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int InternalReadMedia(IntPtr opaque, IntPtr buf, uint len);

        /// <summary>Callback prototype to seek a custom bitstream input media.</summary>
        /// <param name="opaque">private pointer as set by the</param>
        /// <param name="offset">absolute byte offset to seek to</param>
        /// <returns>0 on success, -1 on error.</returns>
        /// <remarks>callback</remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int InternalSeekMedia(IntPtr opaque, ulong offset);

        /// <summary>Callback prototype to close a custom bitstream input media.</summary>
        /// <param name="opaque">private pointer as set by the</param>
        /// <remarks>callback</remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void InternalCloseMedia(IntPtr opaque);
        #endregion

        #region Events

        /// <summary>
        /// The meta information changed
        /// </summary>
        public event EventHandler<MediaMetaChangedEventArgs> MetaChanged
        {
            add => EventManager.AttachEvent(EventType.MediaMetaChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaMetaChanged, value);
        }

        /// <summary>
        /// The parsing status changed
        /// </summary>
        public event EventHandler<MediaParsedChangedEventArgs> ParsedChanged
        {
            add => EventManager.AttachEvent(EventType.MediaParsedChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaParsedChanged, value);
        }

        /// <summary>
        /// A sub item was added to this media's MediaList
        /// </summary>
        public event EventHandler<MediaSubItemAddedEventArgs> SubItemAdded
        {
            add => EventManager.AttachEvent(EventType.MediaSubItemAdded, value);
            remove => EventManager.DetachEvent(EventType.MediaSubItemAdded, value);
        }

        /// <summary>
        /// The duration of the media changed
        /// </summary>
        public event EventHandler<MediaDurationChangedEventArgs> DurationChanged
        {
            add => EventManager.AttachEvent(EventType.MediaDurationChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaDurationChanged, value);
        }

        /// <summary>
        /// The media was freed on the native side
        /// </summary>
        public event EventHandler<MediaFreedEventArgs> MediaFreed
        {
            add => EventManager.AttachEvent(EventType.MediaFreed, value);
            remove => EventManager.DetachEvent(EventType.MediaFreed, value);
        }

        /// <summary>
        /// The media state changed
        /// </summary>
        public event EventHandler<MediaStateChangedEventArgs> StateChanged
        {
            add => EventManager.AttachEvent(EventType.MediaStateChanged, value);
            remove => EventManager.DetachEvent(EventType.MediaStateChanged, value);
        }

        /// <summary>
        /// A sub item tree was added to this media
        /// </summary>
        public event EventHandler<MediaSubItemTreeAddedEventArgs> SubItemTreeAdded
        {
            add => EventManager.AttachEvent(EventType.MediaSubItemTreeAdded, value);
            remove => EventManager.DetachEvent(EventType.MediaSubItemTreeAdded, value);
        }

        #endregion
    }

    #region enums

    /// <summary>Note the order of libvlc_state_t enum must match exactly the order of</summary>
    /// <remarks>
    /// <para>mediacontrol_PlayerStatus,</para>
    /// <para>input_state_e enums,</para>
    /// <para>and VideoLAN.LibVLCSharp.State (at bindings/cil/src/media.cs).</para>
    /// <para>Expected states by web plugins are:</para>
    /// <para>IDLE/CLOSE=0, OPENING=1, PLAYING=3, PAUSED=4,</para>
    /// <para>STOPPING=5, ENDED=6, ERROR=7</para>
    /// </remarks>
    public enum VLCState
    {
        /// <summary>
        /// Nothing special happening
        /// </summary>
        NothingSpecial = 0,

        /// <summary>
        /// Opening media
        /// </summary>
        Opening = 1,

        /// <summary>
        /// Buffering media
        /// </summary>
        Buffering = 2,

        /// <summary>
        /// Playing media
        /// </summary>
        Playing = 3,

        /// <summary>
        /// Paused media
        /// </summary>
        Paused = 4,

        /// <summary>
        /// Stopped media
        /// </summary>
        Stopped = 5,

        /// <summary>
        /// Ended media
        /// </summary>
        Ended = 6,

        /// <summary>
        /// Error media
        /// </summary>
        Error = 7
    }

    /// <summary>
    /// Media track type such as Audio, Video or Text
    /// </summary>
    public enum TrackType
    {
        /// <summary>
        /// Unknown track
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Audio track
        /// </summary>
        Audio = 0,

        /// <summary>
        /// Video track
        /// </summary>
        Video = 1,

        /// <summary>
        /// Text track
        /// </summary>
        Text = 2
    }

    /// <summary>
    /// Video orientation
    /// </summary>
    public enum VideoOrientation
    {
        /// <summary>Normal. Top line represents top, left column left.</summary>
        TopLeft = 0,
        /// <summary>Flipped horizontally</summary>
        TopRight = 1,
        /// <summary>Flipped vertically</summary>
        BottomLeft = 2,
        /// <summary>Rotated 180 degrees</summary>
        BottomRight = 3,
        /// <summary>Transposed</summary>
        LeftTop = 4,
        /// <summary>Rotated 90 degrees clockwise (or 270 anti-clockwise)</summary>
        LeftBottom = 5,
        /// <summary>Rotated 90 degrees anti-clockwise</summary>
        RightTop = 6,
        /// <summary>Anti-transposed</summary>
        RightBottom = 7
    }

    /// <summary>
    /// Video projection
    /// </summary>
    [Flags]
    public enum VideoProjection
    {
        /// <summary>
        /// Rectangular
        /// </summary>
        Rectangular = 0,
        /// <summary>360 spherical</summary>
        Equirectangular = 1,

        /// <summary>
        /// Cubemap layout standard
        /// </summary>
        CubemapLayoutStandard = 256
    }

    /// <summary>Type of a media slave: subtitle or audio.</summary>
    public enum MediaSlaveType
    {
        /// <summary>
        /// Subtitle
        /// </summary>
        Subtitle = 0,

        /// <summary>
        /// Audio
        /// </summary>
        Audio = 1
    }

    /// <summary>
    /// Meta data types
    /// </summary>
    public enum MetadataType
    {
        /// <summary>
        /// Title metadata
        /// </summary>
        Title = 0,

        /// <summary>
        /// Artist metadata
        /// </summary>
        Artist = 1,

        /// <summary>
        /// Genre metadata
        /// </summary>
        Genre = 2,

        /// <summary>
        /// Copyright metadata
        /// </summary>
        Copyright = 3,

        /// <summary>
        /// Album metadata
        /// </summary>
        Album = 4,

        /// <summary>
        /// Track number metadata
        /// </summary>
        TrackNumber = 5,

        /// <summary>
        /// Description metadata
        /// </summary>
        Description = 6,

        /// <summary>
        /// Rating metadata
        /// </summary>
        Rating = 7,

        /// <summary>
        /// Date metadata
        /// </summary>
        Date = 8,

        /// <summary>
        /// Setting metadata
        /// </summary>
        Setting = 9,

        /// <summary>
        /// URL metadata
        /// </summary>
        URL = 10,

        /// <summary>
        /// Language metadata
        /// </summary>
        Language = 11,

        /// <summary>
        /// Now playing metadata
        /// </summary>
        NowPlaying = 12,

        /// <summary>
        /// Publisher metadata
        /// </summary>
        Publisher = 13,

        /// <summary>
        /// Encoded by metadata
        /// </summary>
        EncodedBy = 14,

        /// <summary>
        /// Artwork URL metadata
        /// </summary>
        ArtworkURL = 15,

        /// <summary>
        /// Track ID metadata
        /// </summary>
        TrackID = 16,

        /// <summary>
        /// Total track metadata
        /// </summary>
        TrackTotal = 17,

        /// <summary>
        /// Director metadata
        /// </summary>
        Director = 18,

        /// <summary>
        /// Season metadata
        /// </summary>
        Season = 19,

        /// <summary>
        /// Episode metadata
        /// </summary>
        Episode = 20,

        /// <summary>
        /// Show name metadata
        /// </summary>
        ShowName = 21,

        /// <summary>
        /// Actors metadata
        /// </summary>
        Actors = 22,

        /// <summary>
        /// Album artist metadata
        /// </summary>
        AlbumArtist = 23,

        /// <summary>
        /// Disc number metadata
        /// </summary>
        DiscNumber = 24,

        /// <summary>
        /// Disc total metadata
        /// </summary>
        DiscTotal = 25
    }

    /// <summary>
    /// The FromType enum is used to drive the media creation.
    /// A media is usually created using a string, which can represent one of 3 things: FromPath, FromLocation, AsNode.
    /// </summary>
    public enum FromType
    {
        /// <summary>
        /// Create a media for a certain file path.
        /// </summary>
        FromPath,
        /// <summary>
        /// Create a media with a certain given media resource location,
        /// for instance a valid URL.
        /// note To refer to a local file with this function,
        /// the file://... URI syntax <b>must</b> be used (see IETF RFC3986).
        /// We recommend using FromPath instead when dealing with
        ///local files.
        /// </summary>
        FromLocation,
        /// <summary>
        /// Create a media as an empty node with a given name.
        /// </summary>
        AsNode
    }

    /// <summary>
    /// Parse flags used by libvlc_media_parse_with_options()
    /// </summary>
    /// <remarks>libvlc_media_parse_with_options</remarks>
    [Flags]
    public enum MediaParseOptions
    {
        /// <summary>Parse media if it's a local file</summary>
        ParseLocal = 0,
        /// <summary>Parse media even if it's a network file</summary>
        ParseNetwork = 1,
        /// <summary>Fetch meta and covert art using local resources</summary>
        FetchLocal = 2,
        /// <summary>Fetch meta and covert art using network resources</summary>
        FetchNetwork = 4,
        /// <summary>
        /// Interact with the user (via libvlc_dialog_cbs) when preparsing this item
        /// (and not its sub items). Set this flag in order to receive a callback
        /// when the input is asking for credentials.
        /// </summary>
        DoInteract = 8
    }

    /// <summary>
    /// Parse status used sent by libvlc_media_parse_with_options() or returned by
    /// libvlc_media_get_parsed_status()
    /// </summary>
    /// <remarks>
    /// libvlc_media_parse_with_options
    /// libvlc_media_get_parsed_status
    /// </remarks>
    public enum MediaParsedStatus
    {
        /// <summary>
        /// Parsing was skipped
        /// </summary>
        Skipped = 1,

        /// <summary>
        /// Parsing failed
        /// </summary>
        Failed = 2,

        /// <summary>
        /// Parsing timed out
        /// </summary>
        Timeout = 3,

        /// <summary>
        /// Parsing completed successfully
        /// </summary>
        Done = 4
    }

    /// <summary>Media type</summary>
    /// <remarks>libvlc_media_get_type</remarks>
    public enum MediaType
    {
        /// <summary>
        /// Unknown media type
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// File type
        /// </summary>
        File = 1,

        /// <summary>
        /// Directory type
        /// </summary>
        Directory = 2,

        /// <summary>
        /// Disc type
        /// </summary>
        Disc = 3,

        /// <summary>
        /// Stream type
        /// </summary>
        Stream = 4,

        /// <summary>
        /// Playlist type
        /// </summary>
        Playlist = 5
    }

    #endregion
}
