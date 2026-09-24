using _Multiplayer._Program.Services.Analytics.Client.Interfaces;

namespace _Multiplayer._Program.Features.RacingCarPickMenu
{
    public class CarPickShowData : IShowScreenEvent
    {
        public string Placement { get; set; }
        public bool SendScreenEvents => true;
        public RacingCarPickContext CarPickContext;
        public float TimeRemaining;
    }
}