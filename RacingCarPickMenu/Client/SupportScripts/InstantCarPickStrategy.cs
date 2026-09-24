using System;
using System.Threading;
using _Multiplayer._Program.Features.MatchMaking.Client;
using _Multiplayer._Program.Features.RacingCarPickMenu.Client.Interfaces;
using _Multiplayer._Program.Features.RacingCarPickMenu.Client.Presenters;
using _Project._Program.Handlers;
using MVP;
using R3;
using UnityEngine.UI;

namespace _Multiplayer._Program.Features.RacingCarPickMenu.Client.SupportScripts
{
    public class InstantCarPickStrategy : ICarPickStrategy
    {
        private readonly ClientMatchMakingService _clientMatchMakingService;
        private readonly INavigatorAnalytics _analytics;
        private readonly string _placement;

        private CancellationTokenSource _cts;
        private RacingCarPickMenuPresenter _presenter;

        public InstantCarPickStrategy(
            ClientMatchMakingService clientMatchMakingService,
            INavigatorAnalytics analytics, 
            string placement
        )
        {
            _clientMatchMakingService = clientMatchMakingService;
            _analytics = analytics;
            _placement = placement;
        }

        public void Handle(RacingCarPickMenuPresenter presenter, CompositeDisposable disposables)
        {
            _presenter = presenter;

            if (!TryGetButtons(out Button beginButton, out Button stopButton))
                return;

            stopButton.gameObject.SetActive(false);
            beginButton.gameObject.SetActive(true);

            beginButton.OnClickAsObservable()
                .Subscribe(_ => EnterRacing())
                .AddTo(disposables);
        }

        public void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async void EnterRacing()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                if (!TryGetBeginButton(out var beginButton))
                    return;

                beginButton.interactable = false;

                await _presenter.WaitSave(_cts.Token);

                if (!TryGetBeginButton(out _))
                    return;

                _analytics.LogNavigationStageRun(_placement, AnalyticsHandler.MultiStageRacing);
                _clientMatchMakingService.EnterRacingMatch();
            }
            catch (Exception)
            {
                // Ignore
            }
            finally
            {
                if (TryGetBeginButton(out var beginButton))
                    beginButton.interactable = true;
            }
        }

        private bool TryGetBeginButton(out Button beginButton)
        {
            beginButton = null;

            if (_presenter?.View == null || !_presenter.View.BeginButton)
                return false;

            beginButton = _presenter.View.BeginButton;

            return true;
        }

        private bool TryGetButtons(out Button beginButton, out Button stopButton)
        {
            beginButton = null;
            stopButton = null;

            if (_presenter?.View == null || !_presenter.View.BeginButton || !_presenter.View.StopButton)
                return false;

            beginButton = _presenter.View.BeginButton;
            stopButton = _presenter.View.StopButton;

            return true;
        }
    }
}