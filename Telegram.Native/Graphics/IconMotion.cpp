#include "pch.h"
#include "IconMotion.h"

#include <algorithm>
#include <cmath>
#include <iterator>

namespace Graphics3D
{
    namespace
    {
        // idleDelay for both models, from attach, release and the end of every trick.
        constexpr float IdleDelaySeconds = 2.0f;

        // onScroll adds `distance * 0.5` and `distance * 0.05` in raw pixels. A logical pixel here
        // is a density-independent one, so this assumes the xxhdpi density of 3 the phone is
        // designed at - the one number in this file that is ours rather than read.
        constexpr float Density = 3.0f;
        constexpr float YawPerPixel = 0.5f * Density;
        constexpr float PitchPerPixel = 0.05f * Density;

        // sleepAnimation's lift: ofFloat(0, 2, -3, 2, -1, 2, -3, 2, -1, 0), evenly spaced.
        constexpr float WobbleKeys[] = { 0, 2, -3, 2, -1, 2, -3, 2, -1, 0 };

        // CubicBezierInterpolator, including its Newton solve for x, so the curves land on the
        // same values the phone's do.
        float CubicBezier(float x1, float y1, float x2, float y2, float time)
        {
            const float cx = 3 * x1;
            const float bx = 3 * (x2 - x1) - cx;
            const float ax = 1 - cx - bx;

            float x = time;
            for (int i = 1; i < 14; i++)
            {
                const float z = x * (cx + x * (bx + x * ax)) - time;
                if (std::fabs(z) < 1e-3f)
                {
                    break;
                }

                x -= z / (cx + x * (2 * bx + 3 * ax * x));
            }

            const float cy = 3 * y1;
            const float by = 3 * (y2 - y1) - cy;
            const float ay = 1 - cy - by;

            return x * (cy + x * (by + x * ay));
        }
    }

    IconMotion::IconMotion(bool coin)
        : m_coin(coin)
        , m_random(std::random_device{}())
    {
        // animationsCount: one for the coin, five for the star - index 0 is the pull, 1 the slow
        // flip, 2 the doze and 3 and 4 both the quick flip, so that one comes up twice a deck.
        m_deckSize = coin ? 1 : 5;
        for (int i = 0; i < m_deckSize; i++)
        {
            m_deck[i] = i;
        }

        Shuffle();

        // onAttachedToWindow.
        ScheduleIdle();
    }

    float IconMotion::Interpolate(Curve curve, float t)
    {
        switch (curve)
        {
        case Curve::Standard:
            return CubicBezier(0.25f, 0.1f, 0.25f, 1, t);
        case Curve::EaseOut:
            return CubicBezier(0, 0, 0.58f, 1, t);
        case Curve::EaseOutQuint:
            return CubicBezier(0.23f, 1, 0.32f, 1, t);
        case Curve::Overshoot:
        {
            constexpr float tension = 2.0f;
            t -= 1.0f;
            return t * t * ((tension + 1.0f) * t + tension) + 1.0f;
        }
        default:
            return t;
        }
    }

    int IconMotion::Random(int bound)
    {
        return std::uniform_int_distribution<int>(0, bound - 1)(m_random);
    }

    void IconMotion::Shuffle()
    {
        std::shuffle(m_deck.begin(), m_deck.begin() + m_deckSize, m_random);
    }

    void IconMotion::Enter(float delaySeconds)
    {
        // startEnterAnimation ignores its angle argument and always starts from -180.
        m_values[X] = -180;
        m_enterIn = delaySeconds;
    }

    void IconMotion::Press()
    {
        // onDown: everything stops where it is, and nothing idles until the release.
        Cancel();
        m_idleIn = -1;
    }

    void IconMotion::Drag(float dx, float dy)
    {
        m_values[X] += dx * YawPerPixel;
        m_values[Y] += dy * PitchPerPixel;
    }

    void IconMotion::Release()
    {
        StartBack();
    }

    void IconMotion::Tap(float x, float y)
    {
        // onSingleTapUp. It arrives after the release has already started the spring back, and
        // replaces it - unless the model is turned too far, when the spring is let run instead.
        const float toX = (40 + Random(30)) * x;
        const float toY = (40 + Random(30)) * y;

        Cancel();

        if (std::fabs(m_values[X]) > 10)
        {
            StartBack();
            return;
        }

        m_idleIn = -1;

        Add(X, m_values[X], toX, 0, 0.22f, Curve::EaseOutQuint);
        Add(X, toX, 0, 0.22f, 0.6f, Curve::Overshoot);
        Add(Y, m_values[Y], toY, 0, 0.22f, Curve::EaseOutQuint);
        Add(Y, toY, 0, 0.22f, 0.6f, Curve::Overshoot);
        Play();
    }

    void IconMotion::Add(Channel channel, float from, float to, float delay, float duration, Curve curve)
    {
        if (m_trackCount < static_cast<int>(m_tracks.size()))
        {
            m_tracks[m_trackCount++] = { channel, curve, from, to, delay, duration };
        }
    }

    void IconMotion::Play()
    {
        m_setTime = 0;
        m_setRunning = true;
    }

    void IconMotion::Cancel()
    {
        m_backRunning = false;
        m_setRunning = false;
        m_trackCount = 0;
    }

    void IconMotion::StartBack()
    {
        // startBackAnimation: from wherever it is to rest, all three together, and the next
        // trick queued from now rather than from when the spring ends.
        Cancel();

        for (int i = 0; i < 3; i++)
        {
            m_backFrom[i] = m_values[i];
        }

        m_backTime = 0;
        m_backRunning = true;

        ScheduleIdle();
    }

    void IconMotion::ScheduleIdle()
    {
        m_idleIn = IdleDelaySeconds;
    }

    void IconMotion::StartIdle()
    {
        const int trick = m_deck[m_deckNext++];

        if (m_deckNext >= m_deckSize)
        {
            Shuffle();
            m_deckNext = 0;
        }

        switch (trick)
        {
        case 0:
            Pull();
            break;
        case 1:
            SlowFlip();
            break;
        case 2:
            Sleep();
            break;
        default:
            Flip();
            break;
        }
    }

    void IconMotion::Pull()
    {
        const int variant = Random(4);

        if (variant == 0 && !m_coin)
        {
            // A nod: tipped back and dropped forward again.
            Add(Y, m_values[Y], 48, 0, 2.3f, Curve::EaseOutQuint);
            Add(Y, 48, 0, 2.3f, 0.5f, Curve::Overshoot);
        }
        else
        {
            // A spin of a turn and a third, or one turn for the coin, either way round. The
            // first animator starts from angleY, not angleX - the original's slip, kept because
            // at rest both are zero and it is the original.
            const float turn = m_coin ? 360.0f : 485.0f;
            const float to = variant == 2 ? -turn : turn;

            Add(X, m_values[Y], to, 0, 3.0f, Curve::EaseOutQuint);
            Add(X, to, 0, 3.0f, 1.0f, Curve::Overshoot);
        }

        Play();
    }

    void IconMotion::SlowFlip()
    {
        Add(X, m_values[X], 360, 0, 8.0f, Curve::Standard);
        Play();
    }

    void IconMotion::Flip()
    {
        Add(X, m_values[X], 180, 0, 0.6f, Curve::Standard);
        Add(X, 180, 360, 2.0f, 0.6f, Curve::Standard);
        Play();
    }

    void IconMotion::Sleep()
    {
        // Turned over and tipped back, bobbing for ten seconds, then sprung upright. The return
        // starts from 180 and 60 rather than the 184 and 50 it went to, as the original does.
        Add(X, m_values[X], 184, 0, 0.6f, Curve::EaseOut);
        Add(Y, m_values[Y], 50, 0, 0.6f, Curve::EaseOut);
        Add(X2, 0, 0, 0, 10.0f, Curve::Wobble);
        Add(X, 180, 0, 10.0f, 0.8f, Curve::Overshoot);
        Add(Y, 60, 0, 10.0f, 0.8f, Curve::Overshoot);
        Play();
    }

    void IconMotion::Advance(float step)
    {
        if (m_enterIn >= 0)
        {
            m_enterIn -= step;

            if (m_enterIn < 0)
            {
                StartBack();
            }
        }

        if (m_idleIn >= 0)
        {
            m_idleIn -= step;

            if (m_idleIn < 0)
            {
                if (m_setRunning || m_backRunning)
                {
                    ScheduleIdle();
                }
                else
                {
                    StartIdle();
                }
            }
        }

        if (m_backRunning)
        {
            m_backTime += step;

            const float t = std::min(m_backTime / 0.6f, 1.0f);
            const float remaining = 1.0f - Interpolate(Curve::Overshoot, t);

            for (int i = 0; i < 3; i++)
            {
                m_values[i] = m_backFrom[i] * remaining;
            }

            if (t >= 1)
            {
                m_backRunning = false;
            }
        }

        if (m_setRunning)
        {
            m_setTime += step;

            // Tracks are added in start order per channel, so a later one that has begun
            // overrides an earlier one that has ended - which is how a value holds between the
            // two halves of a trick, as an animator that has finished simply stops updating.
            bool done = true;

            for (int i = 0; i < m_trackCount; i++)
            {
                const Track& track = m_tracks[i];

                if (m_setTime < track.delay + track.duration)
                {
                    done = false;
                }

                if (m_setTime < track.delay)
                {
                    continue;
                }

                const float t = std::min((m_setTime - track.delay) / track.duration, 1.0f);

                if (track.curve == Curve::Wobble)
                {
                    constexpr int segments = static_cast<int>(std::size(WobbleKeys)) - 1;
                    const float position = t * segments;
                    const int k = std::min(static_cast<int>(position), segments - 1);

                    m_values[track.channel] = WobbleKeys[k] + (WobbleKeys[k + 1] - WobbleKeys[k]) * (position - k);
                }
                else
                {
                    m_values[track.channel] = track.from + (track.to - track.from) * Interpolate(track.curve, t);
                }
            }

            if (done)
            {
                // onAnimationEnd: every trick ends a whole number of turns round, so the reset
                // is invisible and only keeps the angle from growing.
                m_values[X] = 0;
                m_setRunning = false;
                m_trackCount = 0;

                ScheduleIdle();
            }
        }
    }
}
