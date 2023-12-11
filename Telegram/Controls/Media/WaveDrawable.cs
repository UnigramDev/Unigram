namespace Telegram.Controls.Media
{
    public class WaveDrawable
    {
        public const float MAX_AMPLITUDE = 1800f;

        private const float ROTATION_SPEED = 0.36f * 0.1f;
        public const float SINE_WAVE_SPEED = 0.81f;
        public const float SMALL_WAVE_RADIUS = 0.55f;
        public const float SMALL_WAVE_SCALE = 0.40f;
        public const float SMALL_WAVE_SCALE_SPEED = 0.60f;
        public const float FLING_DISTANCE = 0.50f;
        private const float WAVE_ANGLE = 0.03f;
        private const float RANDOM_RADIUS_SIZE = 0.3f;

        private const float ANIMATION_SPEED_CIRCLE = 0.45f;
        public const float CIRCLE_ALPHA_1 = 0.30f;
        public const float CIRCLE_ALPHA_2 = 0.15f;

        private const float IDLE_ROTATION_SPEED = 0.2f;
        private const float IDLE_WAVE_ANGLE = 0.5f;
        private const float IDLE_SCALE_SPEED = 0.3f;
        private const float IDLE_RADIUS = 0.56f;
        private const float IDLE_ROTATE_DIF = 0.1f * IDLE_ROTATION_SPEED;

        private const float ANIMATION_SPEED_WAVE_HUGE = 0.65f;
        private const float ANIMATION_SPEED_WAVE_SMALL = 0.45f;
        private const float animationSpeed = 1f - ANIMATION_SPEED_WAVE_HUGE;
        private const float animationSpeedTiny = 1f - ANIMATION_SPEED_WAVE_SMALL;
        public const float animationSpeedCircle = 1f - ANIMATION_SPEED_CIRCLE;
    }
}
