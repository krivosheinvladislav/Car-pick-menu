using _Multiplayer._Program.Features.RacingCarPickMenu.Client.Presenters;
using R3;

namespace _Multiplayer._Program.Features.RacingCarPickMenu.Client.Interfaces
{
    public interface ICarPickStrategy
    {
        public void Handle(RacingCarPickMenuPresenter presenter, CompositeDisposable disposables);
        public void Cancel();
    }
}