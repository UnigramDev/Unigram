# AnimatedImage: one clock per window

Plan for replacing the two clocks that drive `AnimatedImage` with the compositor clock alone.
The current design and its traps are in `architecture.md`, section *Animated images*.

## Why

Two loops that know nothing about each other decide what is on screen:

- `AnimationScheduler`, process-wide, decides when a presenter **decodes**: a thread-pool timer
  per batch, period `(int)(1000 / fps)`, so 62.5 Hz for a 60 fps animation, with timer jitter.
- `FrameFlusher`, one per window, decides when it is **drawn**: the compositor clock, 60 Hz,
  with up to `CommitsInFlight` commits unconfirmed.

A presenter has two buffers, one on screen and one free or holding the next frame, so it can
only decode once the flusher has drawn its last frame and handed a buffer back. When two
timer ticks land between two passes, the second finds no buffer; when a later interval gets no
tick, that pass has nothing new. Nothing makes the frame up:

- Lottie, Dice and Particles advance one frame per decode, so a lost decode is lost time and
  the animation plays slow. The poll rate is clamped to 30-60, so a 24 fps Lottie plays 25% fast.
- Video paces on frame timestamps but cannot skip, so it only ever falls further behind.

Measured on a Release build, 2026-10-15, a 60 fps screen recording in a message (640x491 out):

| | Commit gate of 1 | `CommitsInFlight` = 3 |
|---|---|---|
| Flusher passes/s | ~30 | 59-61 |
| Frames rendered/s | ~30 | 51-55 |
| Ticks/s with no free buffer | ~32 | 7-12 |
| Playback speed | 0.50 | 0.84-0.91 |
| Lag after ~14 s | 6.5 s, growing | 1.7 s, growing |

Decode was 2.2-3.3 ms a frame and commits are confirmed 22-42 ms after they are requested,
waited on, not worked on. The machine is idle; the frames are lost to phase, not to cost.

## Design

**The flusher becomes the frame loop**, one per window, on the thread it has now. Each vblank:

1. Wait for the compositor clock, then for fewer than `CommitsInFlight` unconfirmed commits.
2. Draw every presenter whose next frame is ready; one commit if anything was drawn.
3. Plan the next vblank: for each playing presenter, which frame belongs at the next vblank's
   time. When it differs from the one on screen, queue its decode into the free buffer.

Decodes start right after the pass, with most of a frame to finish. One that misses its vblank
leaves the current frame up, and the next plan asks for a later time, so a late presenter skips
a frame instead of slowing down.

**Tasks answer "the frame for time t"** rather than "the next frame":

- Lottie and Dice: index = time x the animation's frame rate. Markers still stop playback on
  the frame before them, and the end still wraps into `Loop`. Both have random access, live
  and from the cache.
- Video: native `RenderSync` takes a time to skip to. Frames before it are decoded, since
  H.264 needs them as references, but not scaled or copied. From the cache it moves the index.
- Particles is a simulation stepped once per call, and stays stepped.

Catch-up is then not a mechanism of its own, every animation plays at its own rate, and on a
120 or 144 Hz display 60 fps content reuses its frame on the vblanks in between, with no decode
and no commit.

**Decodes run on a small fixed pool** shared by all windows, handed one batch per vblank from
reused lists. The borrow gate stays: `Dispose` still races a decode.

**Removed:** `AnimationScheduler` (`AnimatedImagePresenter` is its only user) and its batches,
timers and rebalancing; `LockFreeArrayList`, written for it; the video's `Due()`; Lottie and
Dice's `framesPerUpdate`; the poll-rate clamp; the rule that a task's `FrameRate` must not change
because the scheduler finds its batch by it. A presenter keeps two buffers.

**Kept, reworked:**

- `LimitFps` plans a presenter every other vblank: half the frames, same speed.
- A loop end with `LoopCompleted` listeners blocks its decode thread on `_pausedLock` until
  the UI thread answers. On a shared pool that stalls every animation, so it becomes a parked
  state the handler resumes or stops.
- Idle: with nothing playing the loop sleeps on its signal, as now. A minimized window is
  still bounded by `CommitsInFlight`.
- Device loss and redraw, `PositionChanged`, marker seeks, Win32 windows sharing a thread.

## Stages

Each is its own commit, measured on a Release build, and updates the architecture section.

1. **Tasks pick frames by time**, with the native video target, still driven by the scheduler:
   each tick asks for the frame at the task's clock. Expected: speed 1.00 for video and Lottie,
   still about 53 distinct frames a second.
2. **The frame loop replaces the scheduler.** Expected: a new frame every vblank.

The meter for each reports, per presenter and second: distinct frames, frames skipped to catch
up, speed (animation time over wall time); and per window: passes, decodes that missed their
vblank, the loop's time per pass, commit latency.

## Clock in stage 1

Each task keeps the clock the video task already had: playback time advanced by the wall time
between calls. A gap of more than four poll intervals - paused, starved behind a stalled UI
thread, waiting for a cache build - resumes on the frame that was next rather than jumping, so
a stall plays as a pause. Smaller shortfalls, the ones that used to accumulate, are skipped.
Stage 2 replaces it with the loop's vblank time.
