#pragma once

#include <array>
#include <cstdint>
#include <random>

namespace Graphics3D
{
    /// <summary>
    /// How the star and the coin move: GLIconTextureView's animators, ported one for one.
    /// </summary>
    /// <remarks>
    /// Kept in the Android view's own terms - angleX turns about the vertical axis, angleY about
    /// the horizontal one, angleX2 lifts the model in model units - so that it can be read against
    /// GLIconTextureView.java line by line. IconScene turns those into a matrix exactly as
    /// GLIconRenderer.onDrawFrame does.
    ///
    /// At rest it is still. Two seconds after anything ends, it plays one of its idle tricks,
    /// drawn from a shuffled deck so that none repeats until all have played: for the star a nod,
    /// a spin, a slow flip, a doze and two quick flips; for the coin only the spin.
    /// </remarks>
    class IconMotion
    {
    public:
        explicit IconMotion(bool coin);

        // The entrance the Premium and Business hosts play: turned half away, then sprung back
        // after the delay.
        void Enter(float delaySeconds);

        void Press();
        void Drag(float dx, float dy);
        void Release();
        void Tap(float x, float y);

        void Advance(float step);

        float AngleX() const noexcept { return m_values[X]; }
        float AngleY() const noexcept { return m_values[Y]; }
        float Lift() const noexcept { return m_values[X2]; }

    private:
        enum Channel : std::uint8_t
        {
            X,
            Y,
            X2
        };

        enum class Curve : std::uint8_t
        {
            Linear,
            Standard,       // CubicBezierInterpolator.DEFAULT
            EaseOut,        // CubicBezierInterpolator.EASE_OUT
            EaseOutQuint,   // CubicBezierInterpolator.EASE_OUT_QUINT
            Overshoot,      // OvershootInterpolator, tension 2
            Wobble          // sleepAnimation's ten linear keyframes
        };

        // One ValueAnimator of the set that is playing. They all start together and the delay
        // is each one's startDelay, as in AnimatorSet.playTogether.
        struct Track
        {
            Channel channel;
            Curve curve;
            float from;
            float to;
            float delay;
            float duration;
        };

        static float Interpolate(Curve curve, float t);

        void Add(Channel channel, float from, float to, float delay, float duration, Curve curve);
        void Play();
        void Cancel();

        void StartBack();
        void ScheduleIdle();
        void StartIdle();

        void Pull();
        void SlowFlip();
        void Sleep();
        void Flip();

        int Random(int bound);
        void Shuffle();

        const bool m_coin;

        float m_values[3] = {};

        // A fixed array rather than a vector: a set never holds more than five animators, and
        // starting one should not allocate.
        std::array<Track, 8> m_tracks{};
        int m_trackCount = 0;
        float m_setTime = 0;
        bool m_setRunning = false;

        // startBackAnimation: a separate animator, not part of the set.
        float m_backFrom[3] = {};
        float m_backTime = 0;
        bool m_backRunning = false;

        // Seconds until the idle trick and the entrance's spring, or negative for none.
        float m_idleIn = -1;
        float m_enterIn = -1;

        std::array<int, 5> m_deck{};
        int m_deckSize = 0;
        int m_deckNext = 0;

        std::minstd_rand m_random;
    };
}
