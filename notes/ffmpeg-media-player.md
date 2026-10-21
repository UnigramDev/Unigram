# ffmpeg media player

Replace both libvlc (`AsyncMediaPlayer`) and the WebView2 + hls.js player (`WebVideoPlayer`) with one
player of our own over the ffmpeg already linked into `Telegram.Native`. Nothing hls.js gives today
may be lost: automatic quality that adapts to bandwidth mid-playback, and a manual choice that takes
effect without reloading the video.

Reference: Telegram Desktop, `C:\Source\tdesktop\Telegram\SourceFiles\` at `3a15bf1fe3` (below,
`S/` = `media/streaming/`, `A/` = `media/audio/`, `FF` = `ffmpeg/ffmpeg_utility.cpp`). Its design is
the starting point; where this note departs from it, it says why.

Status, 2026-10-20:
- **Phase 1:** built, tested on the corpus and committed (`c876ac3ab`).
- **Phase 2:** built, tested and committed (`5af80ddd4`).
- **Phase 3:** built, tested and committed (`f24deba0c`).
- **Phase 4:** built, tested and committed (`34d3de2d3`).
- **Phase 5:** built, tested and committed (`e42af525a`).
- **Phase 6:** libvlc and `WebVideoPlayer` removed in one commit; see "Phase 6 as built" below.


## What we have today

**Three media stacks:** libvlc for the gallery, stories, music, voice and video notes and the media
editor; WebView2 + hls.js for videos with alternative qualities; ffmpeg directly for `VideoAnimation`
(GIFs, video stickers). The goal is one stack: ffmpeg.

**`AsyncMediaPlayer` consumers** (the surface the replacement has to cover):

| Consumer | Plays | Uses beyond play/pause |
|---|---|---|
| `NativeVideoPlayer` (gallery) | `RemoteFileSource`, video | relative `Seek`, `Position` bursts while scrubbing, `Rate` 0.2–2.5, `Volume`, `Mute`, looping via `EndReached`, `VideoOut` → first frame, `StreamSelected` → size; moved between windows for PiP under `IsUnloadedExpected` |
| `StoryContent` | `RemoteFileSource` (non-adaptive) | `Buffering` 100 → hide skeleton and mark opened, `Pause(bool)`, `Context.Clear()`, `EndReached` → next story |
| `PlaybackService` | music, voice notes, video notes | one player swapping sources (queue, shuffle, repeat in C#); `Position` read straight back after a set; `StateChanged` drives advance and failure; `Buffering` 100 marks a note listened; the swap chain moves between the bubble and `PlaybackOverlay`; follows the default audio device |
| `EditMediaPopup` | `LocalFileSource` | trim loop by `PositionChanged`; **seeks while paused and expects the frame to update** |
| `VideoPreloader` | video, no swap chain | warms TDLib's download of the head; behind `Diagnostics.VideoPreloadDebug`, default off |

`MediaHttpServer` has no callers left. `Play(Uri)`, absolute `Seek`, `Log`, `EncounteredError` and
`Options.Arguments` are unused.

**`WebVideoPlayer`** serves hls.js a `master.m3u8` built from the h264 alternatives, each
alternative's TDLib playlist with `mtproto:<id>` rewritten to the mp4, and the byte ranges through
`RemoteFileSource`. `hls.html` starts at level 0, ABR on; a manual pick sets `autoLevelEnabled = false`.

**The ffmpeg build** (`Libraries/vcpkg-ports/ffmpeg/portfile.cmake`) starts from `--disable-everything`
(line 519, inside the `vpx` feature branch, which `vcpkg.json` turns on). Confirmed against
`config_components.h` of the local arm64 build:

- **Decoders:** h264, hevc, av1 + libdav1d, mpeg4, vp8, libvpx_vp8/vp9 (no native vp9), aac, mp3,
  opus, vorbis, flac, ac3/eac3, alac, wma, pcm. **Demuxers:** mov, matroska, ogg, mp3, aac, flac, wav,
  w64, h264, hevc, m4v, gif. No HLS or MPEG-TS demuxer, which this design does not need.
- **No hwaccel.** `--enable-d3d11va` (line 44) turns on the API only; `CONFIG_H264_D3D11VA_HWACCEL`
  and the HEVC one are 0.
- **No filters.** `CONFIG_ATEMPO_FILTER` is 0. libavfilter and libswresample are *built* (port default
  features) but not linked: `Directory.Build.targets:22-24` links avcodec, avformat, avutil and
  swscale only.

**Reusable native pieces:**
- `IVideoAnimationSource`/`RemoteFileSource`: `ReadCallback` blocks until bytes are on disk, then the
  native side reads `FilePath` itself. 0 means end of stream; -1 is the 30 s stall error.
- `AsyncMediaPlayerSwapChain`: device selection, composition swap chain, `Attach` that moves one
  swap chain between panels across windows, size and scale plumbing. libvlc presents into it today.
- `SoundPlayer`: an XAudio2 engine on a worker thread with critical-error handling. Not a streaming
  sink, but the engine and worker pattern carry over.
- `VideoTranscoder`: creates a D3D11 video device and runs an `ID3D11VideoProcessor`.
- `AudioPitchEffect` is a stub (fixed 0.5 pitch shift, mono 48 kHz, reached from nothing). It is not a
  tempo changer; ignore it.


## What the alternative videos actually are

Read from TDLib's cache of real playlists (`LocalState\0\documents\mtproto <id>`):

```
#EXT-X-TARGETDURATION:5
#EXT-X-INDEPENDENT-SEGMENTS
#EXT-X-MAP:URI="mtproto:5348224269186798841",BYTERANGE="1444@0"
#EXTINF:5
#EXT-X-BYTERANGE:686793@1576
mtproto:5348224269186798841
...
```

- Each quality is **one fragmented mp4**. The init section (ftyp + moov, ~1.4 KB) is at offset 0 and
  each segment is one moof + mdat of 5 s. The moov is at the head, so there is no tail fetch.
- **Segment boundaries line up across qualities:** the playlists of one video have identical
  `EXTINF` sequences, with only the byte sizes differing. Four sets were checked.
- So every 5 s boundary is a keyframe in every rendition, and a switch there is clean. hls.js and
  ExoPlayer rely on exactly this. **TDesktop does not:** it ignores the playlists and switches by
  reopening the other file at the current position.


## How TDesktop does it

**Bytes** (`S/media_streaming_file.cpp`, `S/media_streaming_reader.cpp`):
- **AVIO:** a 4 KB buffer with `protocol_whitelist = ""`, so the HLS/DASH demuxers cannot open URLs
  (FF:343-349). Seeks are backward-only with `AVFMT_FLAG_FAST_SEEK` set.
- **Read callback:** all-or-nothing. It blocks on a semaphore until the reader has the bytes. Before
  blocking it flushes the packets already demuxed to the decoders and reports "waiting for data"
  (file.cpp:72-95).
- **Seek callback:** sets the offset only, never does I/O, and answers `AVSEEK_SIZE`.
- **Interrupt:** an atomic flag plus a semaphore release, then a thread join. No ffmpeg
  `interrupt_callback`.
- **One demux thread per play.** Packets are batched per stream. A seek is a full restart: a new
  thread and a new `avformat_open_input`.
- **Read-ahead comes from the demuxer, not the network:** demux runs until audio and video are 32 s
  ahead for a remote file (5 s for local), then sleeps; the network fetches only 1 MB beyond the
  demux position (player.cpp:23-24; reader.cpp:28-29).
- **Sharing:** one reader and one player per document, many UI instances on it
  (`data/data_streaming.cpp:91-148`).

**Clock and output** (`S/media_streaming_player.cpp`, `S/media_streaming_video_track.cpp`):
- **Audio is the master clock.** The position is `AL_SAMPLE_OFFSET`, polled every 100 ms. Video-only
  files use the wall clock from resume.
- **Video:** decodes on a serial queue into a lock-free ring of 4 frames. A stale frame is skipped
  when the next one is ready; otherwise the decoder sleeps until the next frame's time. Late paints
  feed back into the timeline.
- **Pause/resume:** pausing records the time and resume shifts the sync point. Audio pauses at once,
  with no fade.
- **Seeks are precise:** backward to a keyframe, then both tracks decode and drop frames up to the
  target.
- **Speed:** 0.5–2.5 with pitch kept, through an `abuffer → atempo → abuffersink` graph in the audio
  loader (A/media_audio_ffmpeg_loader.cpp:461-568). A speed change re-filters the queued audio onto
  a fresh source. Speed control is disabled entirely if atempo is missing.
- **Stall rule:** when either track runs dry with less than 3 s buffered, the player pauses itself
  and reports waiting. It resumes once every track has `position + min(3 s, duration)` received.
  The spinner shows after 500 ms.
- **Hardware decode:** off by default except on macOS. It tries D3D11 → DXVA2 → CUDA, but **copies
  every GPU frame back to system memory** (`av_hwframe_transfer_data`).
- **Looping:** only without audio. It adds the duration to a looping shift and seeks the demuxer to 0.

**Qualities** (`data/data_document.cpp:550-722`, `S/media_streaming_document.cpp:182-245`,
`media/view/media_view_overlay_widget.cpp:5706-5805`):
- **The list:** the alternatives with video dimensions that streaming can play, sorted by height;
  the smaller file wins a tie. There is no codec check: a file that fails to open is marked and left
  out next time.
- **Bandwidth:** bytes received over the time at least one request was in flight, in a 5 s window,
  recomputed about once a second (`S/media_streaming_loader_mtproto.cpp:143-218`). It is marked
  unreliable below 3 parts.
- **Up:** when at least 4 s are buffered, jump to the largest rendition whose
  `size / duration × playbackSpeed × 1.2` is at most the measured bytes per second. This can skip
  several steps.
- **Down:** one step, only while stalled with the spinner showing.
- **The switch:** grab the current frame, tear down, open a new player on the other file at the
  current position, and show the held frame until the first new one arrives. Speed and volume are
  global.
- **Persistence:** the choice is global, in settings (`{manual, height, original}`). Default
  `{auto, 480}`. **An automatic switch is saved too**, so the next video starts at the height auto
  last chose.
- **The original** is offered (as "Original (Np)") only once it is downloaded. A downloaded original
  always wins.
- **Inline videos** pick by the same setting, capped to what fits the bubble.

**What iOS and Android add** (both drive hls.js or ExoPlayer over the same playlists, so the switch
is in-buffer and seamless):
- **iOS** starts at the best rendition if it is fully downloaded, otherwise at the lowest one
  ≥ 600p. It accepts h264/hevc always, and av1 only behind config flags.
- **Android** drops av1/hevc/vp9 without a hardware decoder for them. It plays the original when it
  is cached, and keeps the manual choice per message.


## Design

The player is native, in `Telegram.Native/Media`, so the decode and present loops never cross the
ABI per frame.

**Public surface: the `AsyncMediaPlayer` contract, plus qualities.** Five consumers already speak it,
along with its threading rules: events marshalled to the creating thread's `DispatcherQueue`, a
`Close` that is safe from any thread, a swap chain that moves between panels. Keeping it makes each
migration a type swap that can sit behind a Diagnostics switch next to libvlc. It also keeps the
behaviours consumers quietly depend on:
- `Position` reads back the value just set;
- `EndReached` forces `position = duration`;
- `Pause`, `Toggle`, `Play` and seek restart an ended player;
- `Buffering` reaches 100 once playback can start.

The parts that only existed for libvlc go (`Arguments`, `Log`, `Play(Uri)`). New members:
`SetRenditions(...)`, `Levels`, `CurrentLevel` (null = auto), `LevelChanged`, `WaitingChanged`.
Unlike `AsyncMediaPlayer` today, `State`, `IsPlaying`, `Rate`, `Volume` and `Mute` are answered from
cached values; no getter blocks on a worker.

### Bytes

- **AVIOContext over `IVideoAnimationSource`,** with the same block-then-`ReadFile` contract that
  `VideoAnimation` and `AsyncMediaPlayer` use today. Nothing new crosses the ABI.
  - **Read:** maps -1 or closed to `AVERROR_EXIT`, true end of file to `AVERROR_EOF`, and never
    returns 0 before the end.
  - **Seek:** sets the offset and answers `AVSEEK_SIZE`; nothing else.
  - **Opening:** `protocol_whitelist = ""`.
- **Prefetch stays `RemoteFilePrefetch`** (30 s of media). It already measures the link, and TDLib
  keeps the downloaded parts on disk, so the 8 MB slice cache in TDesktop's reader has no
  counterpart here.
- **Read-ahead cap:** the demuxer also caps itself at 32 s remote / 5 s local, as TDesktop does, so
  packet memory is bounded.
- **One file, one download window.** TDLib keeps one download offset per file, so two players on the
  same file fight over it. That is true today and PiP avoids it by moving the control. A per-file
  registry like TDesktop's `Data::Streaming` is out of scope until something needs two live players
  on one file.

### Threads

- **Demux:** one thread per player, alive across seeks, unlike TDesktop's per-play thread. A seek is
  `avformat_seek_file` backward plus a flush, not a reopen; a reopen costs a moov parse each time,
  and scrubbing sends seeks in bursts.
  - Seeks are **coalesced**: only the latest pending target is honoured.
  - The demuxer sleeps when the read-ahead cap is reached.
- **Video decode:** one thread. Software decode with frame threads first, hardware later (phase 5).
  Frames go into a ring of 4.
- **Audio:** decode, then `swresample`, then `atempo`, on one thread, into an XAudio2 source voice.
  This uses the `SoundPlayer` engine pattern; the voice follows the default render device, as
  `AsyncMediaPlayer` does.
- **Present:** sleeps on a high-resolution waitable timer until the next frame is due, drops a
  frame when the one after it is already due, then presents with `Present(1, 0)`. The swap chain's
  frame-latency waitable object would pace on vblank instead, but the swap chain is created
  without that flag and is shared with the libvlc path, where nothing would wait on it.
- **Teardown:** interrupt flag, release the waits, join; the same order `AsyncMediaPlayer::Close`
  already uses so it is safe from any thread.

### Clock

- **The audio master** is `IXAudio2SourceVoice::GetState(...).SamplesPlayed`, mapped through the
  tempo. It is exact on every query, unlike TDesktop's 100 ms poll, so there is no publish-and-stamp
  step.
- **Video-only** files use the wall clock from resume.
- **Pause:** stop the voice and freeze the clock. Resume shifts the sync point.
- **Rate** 0.2–2.5 (the gallery slider's range). One `atempo` covers 0.5–100, so below 0.5 the
  filter chains two. A rate change re-filters the queued audio, as TDesktop does.

### Seeking

- **Precise:** seek backward to a keyframe, then decode and drop up to the target. The first frame at
  or past the target is **presented even while paused**. `EditMediaPopup`'s trim handles need this,
  and so does scrubbing.
- **Unknown duration:** seeking is disabled, and VP9/WebM durations are not trusted. Both are traps
  TDesktop documents (file.cpp:21-33, 233-236).

### Presentation

- **Swap chain:** keep `AsyncMediaPlayerSwapChain`'s device, composition swap chain and `Attach`. We
  now present into it ourselves.
- **Colour conversion:** an `ID3D11VideoProcessor` does YUV → BGRA with the stream's matrix and
  range, and scales to the panel. One path serves software frames (uploaded as NV12) and hardware
  ones (phase 5).
- **Hardware frames stay on the GPU.** TDesktop's copy-back exists because its renderer is not D3D11;
  ours is.
- **Device loss** is handled here, which `AsyncMediaPlayerSwapChain` never did (TODO at
  `.cpp:132-134`).

### Waiting and buffering

- **Stall:** when either track runs dry with less than 3 s buffered, pause the clock and raise
  waiting.
- **Resume:** once every track has `position + min(3 s, duration)` demuxed, or is complete.
- **`Buffering` maps onto the same rule:** 100 once playback can start or resume, below that while
  waiting. That preserves what stories and `PlaybackService` read from it.

### Qualities

A rendition is one alternative: its mp4 `File`, its HLS playlist `File` (tiny; downloaded first) and,
from the playlist, the init range and the segments' start times and byte ranges. A video with no
alternatives is a single rendition and none of this runs.

- **Demuxing a rendition:** one `AVFormatContext` per rendition in use, opened over that rendition's
  file. Init section and first fragment are the first ~700 KB, so opening is cheap. Each rendition
  is a separate TDLib file, so their download windows do not interfere.
- **Seamless switch** (shared by auto and manual):
  1. Pick the boundary *B*: the first segment start after the current play position.
  2. Drop the queued packets with pts ≥ *B*.
  3. Seek the target rendition's demuxer to *B*; it is a keyframe in every rendition.
  4. Demux from there.
  5. The decoder is per rendition, because extradata and size differ.
  6. Frames up to *B* still come from the old one, then the new one takes over, so playback never
     stops.
  7. If *B* is beyond what the old rendition has downloaded, the switch has no cost to wait for.
     It goes through the normal stall rule.
- **Switching to or from the original:** the original is not segment-aligned, so this is
  TDesktop's reopen: hold the last frame, open at the current position, and present the held frame
  until the first new one.
- **Auto (ABR):** TDesktop's rule, which is simple and proven.
  - **Throughput:** busy-time bytes over a 5 s window, taken from the `UpdateFile` progress that
    `RemoteFileSource` already tracks.
  - **Up:** when at least 4 s are buffered and `bytes per second ≥ size / duration × rate × 1.2`;
    may skip steps.
  - **Down:** one step after 500 ms of stall.
  - A down-switch is at the next boundary like any other, and a stall usually means that data is
    missing anyway.
- **Start:**
  - a fully downloaded rendition or original wins;
  - otherwise the height auto last chose, saved globally;
  - default 480. This follows TDesktop.
- **Codecs:** h264 and hevc decode today (hevc in software until phase 5); av1 is decodable through
  dav1d. TDesktop's fallback carries over: a rendition that fails to open is marked and dropped from
  the list.
- **UI:** `GalleryTransportControls`' quality menu already binds `Levels`/`CurrentLevel`/
  `IsCurrentLevelAuto`. `VideoPlayerLevel` loses its `JsonObject` constructor and carries the
  rendition instead.


## Port changes

- **Phase 1:** link `avfilter` and `swresample`. Enable the filters `abuffer`, `abuffersink`,
  `atempo`, `aformat`, `aresample`.
- **Phase 5:** enable `h264_d3d11va`, `hevc_d3d11va`, `av1_d3d11va`, `vp9_d3d11va`.
- Record the sizes in `notes/duplicated-libraries.md`, which today lists avfilter as unused.


## Phases

Each lands on its own and is reviewed. libvlc stays until phase 6, so every step can fall back.

1. **Core player.** AVIO, demux, software video, XAudio2 audio with atempo, the clock, precise and
   coalesced seek, the waiting rule, presentation through the video processor.
   - `NativeVideoPlayer` gets it behind a Diagnostics switch; libvlc stays the default.
   - Port changes for the audio filters.
2. **Renditions and ABR.** Playlist parsing, per-rendition demux, the boundary switch, auto, the
   original's reopen path. Multi-quality videos reach the engine only with the Diagnostics switch
   on (Fela's call), so the deletions below wait for the switch to become the default:
   - `WebVideoPlayer`;
   - `hls.html`, `hls.js` and `mp4.html` (all three in `Telegram.csproj`);
   - `Diagnostics.ForceWebView2` and its checkbox;
   - the `force` chain through `GalleryContent.OnFailed` → `GalleryWindow.OpenFile`/`Play`;
   - `GalleryMedia.IsHls`, if the rendition list subsumes it.
3. **Stories and `EditMediaPopup`.** `VideoPreloader` stays on libvlc until phase 6. Its job, warming
   TDLib's download of a video's start, needs no player; when libvlc goes, it becomes a
   `DownloadFile` of the head.
4. **`PlaybackService`.** Music, voice notes, video notes, the swap chain moving to the overlay, and
   the save and restore of the voice-over-music interruption.
5. **D3D11VA hardware decode,** zero-copy into the video processor. Software fallback per stream when
   `get_format` or the first frame fails. **The gallery default flips only after this,** because
   libvlc decodes 4K HEVC originals in hardware today.
6. **Remove libvlc.** `AsyncMediaPlayer` stays as the WinRT surface over the engine. Delete:
   - its libvlc half (`EventContext`, `CleanupManager`, the work queue, the `Backend` option),
     `MediaHttpServer`, the libvlc vcpkg port and the plugins target in `Directory.Build.targets`;
   - the libvlc memories and the duplicated-ffmpeg entry.

   Update `notes/architecture.md` (the media decode section) in the same commit.


## Phase 1 as built

**Files.** All under `Telegram.Native/Media/`:
- `MediaEngine.h/.cpp`: the engine and its per-file `MediaSession`;
- `MediaAudioRenderer.h/.cpp`: XAudio2 output, swresample, atempo and the audio clock;
- `MediaVideoRenderer.h/.cpp`: the video processor path and the libyuv fallback;
- `MediaEngineCommon.h`: the waiter, the clock and the queues.

`AsyncMediaPlayer` delegates every member to the engine when
`AsyncMediaPlayerOptions.Backend` is `FFmpeg`. Only `NativeVideoPlayer` sets it, from
`Diagnostics.UseFFmpegPlayer` ("Use FFmpeg Video Player" on the diagnostics page).

**Threads per file:** demux, video decoder, presenter, audio. A new file's session waits for the
previous session's demux thread to give up the source before opening it, since `Stop` and `Play`
reuse one source object.

**Departures from the design above, and why:**
- **Seeking keeps the demux thread and its `AVFormatContext`.** TDesktop restarts both on every
  seek. Here a seek bumps a serial, flushes the queues, and closes the source if a read is blocked
  in it, which is the only way to end a `RemoteFileSource` wait early. The demuxer reopens the
  source and seeks backward.
- **A stall shorter than `RemoteFileSource`'s 30 s timeout is not an error.** That timeout makes
  the read return -1, which libvlc treated as the end of the input. The engine reads again
  instead, while showing that it waits; only a seek or a close ends the wait.
- **`VideoOut` fires on the first frame presented,** not when an output is created. The thumbnail
  therefore stays up until there is a picture to replace it.
- **Gaps in the audio are filled with silence:** audio starting after the video, or a hole in
  it. The audio clock counts samples played, so without the silence it jumps by the gap and the
  picture skips to catch up.
- **The output follows the default device.** XAudio2's output is rebuilt from what was being
  heard when the default device changes or the engine reports a critical error. A speed change
  goes through the same requeue.

**Not done yet:**
- **10-bit and 4:2:2/4:4:4 video** is converted to 8-bit NV12 by swscale on the decoder thread.
  HDR is therefore shown as SDR without tone mapping. P010 into the video processor comes with
  phase 5.
- **Unmigrated consumers:** stories, the media editor, `PlaybackService` and the preloader still
  construct libvlc players.

## Phase 2 as built

**Decided by Fela:**
- **Auto starts** at the quality it last settled on. That is a global setting,
  `Playback.VideoQuality`, by the short side; it is 480 the first time. A fully downloaded quality
  or original wins.
- **A manual pick** is global until Auto is chosen again (`Playback.VideoQualityManual`).
- **Codecs:** H.264 only.
- **Routing:** behind the Diagnostics switch.

**How a quality is read** (`MediaPlaylist.h/.cpp`). The demuxer opens a virtual file: the init
section, then the real file from segment *k*'s offset to the end (`MediaSegmentMap`). This needs no
`sidx`: zeroing the middle of a cached file showed ffmpeg's own seeking reads through rather than
jumping. The parser was run on all 61 cached playlists, and checked against 52 of the real files:
every segment offset is a `moof`. ffprobe on such a virtual file shows absolute timestamps
(`tfdt`) and a keyframe first.

**Trap:** ffmpeg reports the virtual file's `start_time` as segment *k*'s start, so a segmented
input does not subtract it the way a plain file does. Its timestamps are already the whole video's.

**Switching:**
- **Manual:** immediate, as a seek to the current position with the new quality. The last frame
  stays up until the new one is ready.
- **Auto up:** seamless. The switch happens at the first segment boundary at least 3 s ahead of
  playback, once the demuxer has read into that segment, and only if neither decoder has taken a
  packet of it (`PacketQueue::TrimFrom`). Short of the boundary, whatever lay between the demuxer
  and it would never be read. Queued packets from the boundary on are dropped, the new quality is
  opened at the boundary, and a `Switch` marker makes each decoder drain what it holds and start a
  decoder for the new parameters. The audio renderer carries on: its expected-time trim and its
  silence fill absorb any overlap or gap where the two qualities meet.
- **Auto down:** immediate after 0.5 s of stall. It is also taken seamlessly when the measured speed
  falls below the quality's average rate with under 10 s buffered: at the same boundary as a step
  up, or, if the demuxer has not reached it, at the start of the segment it is reading.
- **Reported** when the video decoder reaches the `Switch` marker, not when the demuxer opens the
  new quality, which can be up to 30 s earlier.
- **Failures:** a quality that fails to open, or whose playlist fails to load, is one automatic
  quality no longer moves to. A pick by hand still tries it.
- **During the first open,** a quality picked by hand ends that open and is opened instead.
- **Up rule:** TDesktop's. The largest quality whose average rate × speed × 1.2 the measured download
  speed covers, once 4 s are buffered.
- **Damping:** no switch within 5 s of another, and no step up within 15 s of a step down.

**Bandwidth** is `RemoteFileSource`'s own EWMA of how fast the end of the downloaded range moves.
It is reached through `IAsyncMediaPlayerRendition.DownloadSpeed`.
- **Only time spent downloading:** an interval counts only if it starts with TDLib short of the
  window last asked for. Once TDLib has the window it waits, and the reader asks for more at the
  media's rate, so time spent waiting would measure the media rather than the link.
- **The end, not the prefix:** `downloaded_prefix_size` is measured from `download_offset`, which
  moves with every read. Counting the prefix after the offset moved counted the whole window as
  new, a few megabytes in a tenth of a second, and auto stepped up into stalls.

**Sources:** one per quality, created on first use, with only the playing one open. Closing the
one left behind cancels its download-ahead. Playlists are downloaded on first use, on the demux
thread, so opening a video costs nothing for the qualities it never plays. Closing the player
cancels a playlist download still waiting (`CancelPlaylist`), which offline would never end.

**Fragmented plain files** are opened with `use_mfra_for=pts`, then probed for their duration.
- **Why the index:** without an index of its fragments, the mov demuxer walks every `moof` to the
  end of the file before it returns. That reads 64 KB at each fragment, chunks from across the
  whole file before the first frame. The `mfra` at the end is that index, and ffmpeg reads it only
  when asked.
- **The trap:** opened from the index, ffmpeg takes the duration from the fragments it parsed, only
  the first one, so a 270 s file comes out as 5 s. Seeks clamp to that, and the download window
  sized from it covers the whole file. ffmpeg 7.1.2 and the master build of 2026-10-19 both do
  this.
- **The probe** (`ProbeFragmentedEnd`): a seek past the end parses each stream's last `moof`, and
  the end is read from the timestamps it lists, without reading samples. It runs only when the moov
  describes no samples (`IsFragmented`).
- **Measured against a plain open:**

  | File | Plain open | Index and probe | Duration |
  |---|---|---|---|
  | 12 MB, 59 fragments | 3.7 MB read | 133 KB + 86 KB | the same, 270.274 s |
  | Corpus `03-fragmented.mp4` | 2.4 MB read | 525 KB + 131 KB | 30.083 s against 30.067 s |

  The first packets are the same, and the 40 other corpus MP4s are untouched.
- **Seeks** land at the start of the fragment holding the target, at most one fragment early.
- **No index:** a file with neither an `mfra` nor a `sidx` still walks.

## Phase 3 as built

**Stories and the media editor:** both now take the backend from the Diagnostics switch.

Stories needed nothing else: `Buffering` 100 when playback can start, and below 100 while
stalled, is what the skeleton reads.

**The editor** gets `AsyncMediaPlayer.SetLoop(start, end)`. The engine checks the end as each frame
becomes due, and seeks back to the start instead of showing the first frame past it. Without video,
the audio clock is checked instead. Before this, the loop waited for a reported position past the
end, which the engine sends four times a second, so playback ran up to 250 ms past the trim. libvlc
ignores the call, and the editor's `PositionChanged` handler stays as its fallback.

**`Stop` and `Close` deactivate the renderer.** A stopped session joins on the thread pool, and its
presenter could still draw a frame after the caller cleared the panel. Stories stop and clear
between stories, so that frame would have flashed.

## Phase 4 as built

**`PlaybackService`** takes the backend from the Diagnostics switch, and the engine needed nothing
new for it. What it relies on maps over as is:
- **`Buffering`** reaching 100 is what marks a note listened.
- **`StateChanged`** (Playing, Ended, Error) drives advance, repeat and failure.
- **Restoring interrupted music** is `Play` + `Position` + `Pause`. The engine takes a seek made
  while the file is opening as where to start.
- **Speed** is set before `Play`, and a new session picks it up. The slider's 0.2x takes two
  chained `atempo` stages.
- **Audio-only playback:** voice notes and music take the clock from the audio, and an attached
  cover picture is skipped rather than decoded as video.
- **Video notes:** the swap chain moving between the bubble and `PlaybackOverlay` goes through
  `AsyncMediaPlayerSwapChain.Attach`. That fires the size hook, so a paused note is drawn again at
  its new size.

**Repeat** used to `Stop` an ended player before `Play`. Both backends restart an ended player on
`Play` alone, and on the engine the `Stop` would reopen the file, leaving a gap at every repeat.

**Not done:** gapless playback between tracks. Neither backend has it. The engine could open the
next track's session ahead of the end and hand the audio over at the boundary.

## Phase 5 as built

**Port:** the `h264`, `hevc` and `av1` D3D11VA hwaccels are enabled, both the `d3d11va` and `d3d11va2`
variants. The engine uses the second, `AV_PIX_FMT_D3D11` with a device context.

**VP9 is left out on purpose.** Hardware VP9 needs ffmpeg's own VP9 decoder. `VideoAnimation`
picks decoders with `avcodec_find_decoder`, so building it would switch video stickers from libvpx
to it, and only libvpx keeps their alpha.

**One device for decoding and drawing.** `MediaVideoRenderer::HardwareDevice()` builds the ffmpeg
device context on the renderer's own D3D11 device, with the swap chain's context mutex as its lock.
ffmpeg requires that lock to be recursive; a Win32 mutex is. A decoded surface is one slice of the
decoder's texture array, and the video processor draws it from there through a cached input view:
no copy, and no CPU. `Present` moved outside that lock, because the decoders now take it to submit
work, and a `Present` can wait for a vblank.

**Decoder:**
- **Hardware setup:** `CreateDecoder` asks for `AV_PIX_FMT_D3D11` with one thread, plus extra surfaces
  for the frame queue and the frame on screen.
- **Software fallback:** when `get_format` gets no GPU format, or the hwaccel fails to start (a
  profile or a size the GPU does not take), the decoder thread starts over in software, with frame
  threads. It resends the same first packet, before any frame is received, so nothing is decoded
  twice.
- **AV1:** on the GPU it uses ffmpeg's own AV1 decoder, which decodes nothing in software; dav1d is
  the software fallback.

**HDR:** frames tagged PQ or HLG are given the matching DXGI colour space, and the video processor
maps them to the SDR output. That fixes the washed-out HDR from phase 1. 10-bit HEVC on the GPU
reaches the processor as P010, never through the CPU. How well a driver maps HDR to SDR varies. A
processor whose enumerator says it cannot convert from PQ or HLG is told the frames are SDR BT.2020
instead: washed out, but still drawn on the GPU.

**CPU drawing path:** it takes a GPU surface only when the processor has failed on one. It copies
the surface back, and brings P010 down to NV12.

## Device loss

Recovered within the session, without reopening the file:

1. **Detect.** The renderer notices the loss on `Present`, the video processor `Blt`,
   `ResizeBuffers`, or any failed draw whose device reports a removed reason. A hardware decoder
   whose work fails asks it to check as well (`CheckDevice`), since a decoder that stops producing
   frames gives the renderer nothing to draw.
2. **Rebuild.** `AsyncMediaPlayerSwapChain::Recreate` builds a new device, context, mutex and swap
   chain on a thread-pool thread, retrying with a doubling wait while the driver is still resetting
   (8 tries from 250 ms). Only swapping them in and `SetSwapChain` run on the panel's thread.
3. **Rebind.** The renderer takes the new objects, drops its views, textures and the held frame, and
   releases the ffmpeg hardware device. It then bumps its generation and calls the engine back.
4. **Reset.** The engine seeks to the current position. That flush drops the frames decoded for
   the old device, and each decoder thread, seeing the new generation, takes a fresh hardware device
   and rebuilds its decoder. Audio pauses only for the seek's preroll.

**Guard against loops:** a second loss within 60 s turns GPU decoding off for that player, since
the hardware decoder may be what brings the device down.

**Traps:**
- **Each ffmpeg device context holds its own handle to the context mutex,** closed in its `free`.
  Decoders keep the old device until their next flush, after the renderer has closed its own
  handle, and waiting on a closed handle is fatal in a UWP process.
- **A frame from the old device is dropped by `Render`.** Drawn, its failure would be taken for the
  processor's, and the renderer would draw on the CPU from then on.
- **At the end, the reset seek is quiet:** it brings the last picture back without reporting a
  state change or the end again.
- **A closed swap chain ends the retries.**

## Phase 6 as built

One commit, so that it reverts as one. libvlc and `WebVideoPlayer` are gone, and the engine is the
only player.

**`AsyncMediaPlayer`** forwards every member to the engine and owns nothing else: no work queue, no
event context, no cleanup manager. It loses:
- `Play(Uri)`, `CanPause` (always true), the `Log` event and `AsyncMediaPlayerLogEventArgs`;
- `AsyncMediaPlayerOptions.Backend`, `LogLevel` and `Arguments`.

**`AsyncMediaPlayerSwapChain`** loses what only libvlc read:
- `SwapChainOptions`;
- the private data that published the context mutex and the target size;
- the 320×240 size it reported without a panel, since the renderer falls back to the swap chain's own
  size.

The adapter choice, the multithread lock and the mutex stay, because the renderer and D3D11VA need
them.

**`WebVideoPlayer`**, `hls.html`, `hls.js` and `mp4.html` are deleted. With them go:
- `Diagnostics.ForceWebView2` and `UseFFmpegPlayer`, and their checkboxes;
- the gallery's `force` chain through `OnFailed`;
- `VideoPlayerBase.Failed` and `Ready`, and `VideoPlayerLevel`'s `JsonObject` constructor.

`GalleryMedia.IsHls` stays: it is what says a video has qualities.

**`VideoPreloader`** asks TDLib for the first 4 MB, which is TDesktop's default prefix, instead of
opening a libvlc player until it reported `Buffering` 100. It is still behind
`Diagnostics.VideoPreloadDebug`.

**The editor's trim loop** is the engine's `SetLoop` alone. The `PositionChanged` fallback that
served libvlc is removed.

**Build:**
- **Gone:** the `libvlc` port, its manifest entry, `libvlc.lib`/`libvlccore.lib` on the link line, the
  plugins target, `MediaHttpServer`, and the crash reporter's `VLCException` and libvlc plugin
  modules.
- **The first build after this** fails once with MSB3030 on `libvlc.dll`. The native project lists
  vcpkg's `bin\*.dll` before its vcpkg install purges libvlc. Building again passes.

## Open questions

- **Public surface:** settled. The `AsyncMediaPlayer` surface stays, with qualities added in
  phase 2.
- **Auto height, manual choice, codecs:** settled, see "Phase 2 as built".
- **Looping with audio:** TDesktop refuses it. We need it only for the gallery's `IsLoopingEnabled`
  and `EditMediaPopup`'s trim loop; both can be a seek at the end rather than a demux-level loop.
