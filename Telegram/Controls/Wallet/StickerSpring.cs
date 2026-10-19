//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Controls.Wallet
{
    /// <summary>
    /// A damped spring stepped from the sticker layer's frame loop, so that every sticker shares
    /// one clock.
    /// </summary>
    /// <remarks>
    /// Not a composition animation: gestures read and retarget these every frame, and composition
    /// cannot be read back.
    /// </remarks>
    public sealed class StickerSpring
    {
        public StickerSpring(float value, float stiffness, float damping)
        {
            Value = value;
            Target = value;
            Stiffness = stiffness;
            Damping = damping;
        }

        public float Value;
        public float Target;
        public float Velocity;
        public float Stiffness;
        public float Damping;

        public StickerSpring Set(float value)
        {
            Value = value;
            Target = value;
            Velocity = 0;
            return this;
        }

        public StickerSpring To(float target)
        {
            Target = target;
            return this;
        }

        public StickerSpring With(float stiffness, float damping)
        {
            Stiffness = stiffness;
            Damping = damping;
            return this;
        }

        /// <summary>
        /// Semi-implicit Euler in small steps: stable for stiff springs at any refresh rate.
        /// </summary>
        public void Step(float dt)
        {
            var c = 2 * Damping * MathF.Sqrt(Stiffness);
            var left = dt;

            if (Velocity == 0 && Value == Target)
            {
                return;
            }

            while (left > 0)
            {
                var h = MathF.Min(left, 1f / 480);
                Velocity += (-Stiffness * (Value - Target) - c * Velocity) * h;
                Value += Velocity * h;
                left -= h;
            }

            // Snapped once it is closer than anything drawn could show, so springs at rest cost
            // nothing a frame.
            if (MathF.Abs(Value - Target) < 1e-4f && MathF.Abs(Velocity) < 1e-3f)
            {
                Value = Target;
                Velocity = 0;
            }
        }

        public bool Resting(float epsilon)
        {
            return MathF.Abs(Value - Target) < epsilon && MathF.Abs(Velocity) < epsilon * 10;
        }

        /// <summary>
        /// For an angle: drops whole turns from both value and target, so nothing visibly changes.
        /// </summary>
        public StickerSpring WrapTurns()
        {
            var turns = MathF.Round(Target / 360) * 360;
            Value -= turns;
            Target -= turns;
            return this;
        }

        /// <summary>
        /// For an angle: heads to the given angle the short way round.
        /// </summary>
        public StickerSpring ToAngle(float angle)
        {
            var d = (angle - Value) % 360;
            if (d > 180)
            {
                d -= 360;
            }

            if (d < -180)
            {
                d += 360;
            }

            Target = Value + d;
            return this;
        }
    }
}
