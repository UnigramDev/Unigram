using System.Numerics;

namespace Telegram.Services.Calls
{
    public partial class VoipVideoStateChangedEventArgs
    {
        public VoipVideoStateChangedEventArgs(bool active, Vector2 frame)
        {
            IsActive = active;
            Frame = frame;
        }

        public bool IsActive { get; init; }

        public Vector2 Frame { get; set; }
    }
}
