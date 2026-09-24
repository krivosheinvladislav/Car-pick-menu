using System;
using System.Threading;
using _Multiplayer._Program.Features.Character.Network;
using _Multiplayer._Program.Features.MatchMaking.Client.Presenters;
using _Multiplayer._Program.Features.RacingCarPickMenu.Client.Interfaces;
using _Multiplayer._Program.Features.RacingCarPickMenu.Client.Presenters;
using _Multiplayer._Program.Helpers.Logger.Interfaces;
using _Multiplayer._Program.Modes.Lobby.Shared;
using _Multiplayer._Program.Modes.Lobby.Shared.Scriptables;
using _Multiplayer._Program.Services.Rpc.Views;
using _Project._Program.Handlers;
using _Project._Program.Utils;
using Cysharp.Threading.Tasks;
using MVP;
using R3;
using UnityEngine.UI;
using VContainer;

namespace _Multiplayer._Program.Features.RacingCarPickMenu
{
    public class DelayedCarPickStrategy : ICarPickStrategy
    {
        private readonly GameModesData _gameModesData;
        private readonly IUINavigator _uiNavigator;
        private readonly float _timeRemaining;
        private readonly IObjectResolver _objectResolver;
        private readonly INavigatorAnalytics _analytics;
        private readonly string _placement;
        private readonly IAppLogger<RacingCarPickMenuPresenter> _logger;
        private CompositeDisposable _disposables;

        private NetworkRpcView _networkRpcView;
        private NetworkCharactersList _networkCharactersList;

        private string _userId;
        private RacingCarPickMenuPresenter _presenter;
        private bool _beginClicked;

        private CancellationTokenSource _cts;

        public DelayedCarPickStrategy(
            GameModesData gameModesData, 
            IUINavigator uiNavigator, 
            float timeRemaining,
            IObjectResolver objectResolver, 
            INavigatorAnalytics analytics,
            string placement,
            IAppLogger<RacingCarPickMenuPresenter> logger
        )
        {
            _gameModesData = gameModesData;
            _uiNavigator = uiNavigator;
            _timeRemaining = timeRemaining;
            _objectResolver = objectResolver;
            _analytics = analytics;
            _placement = placement;
            _logger = logger;
        }

        public void Handle(RacingCarPickMenuPresenter presenter, CompositeDisposable disposables)
        {
            _beginClicked = false;
            _disposables = disposables;
            _networkRpcView = _objectResolver.Resolve<NetworkRpcView>();
            _networkCharactersList = _objectResolver.Resolve<NetworkCharactersList>();

            if (!_networkRpcView || !_networkCharactersList)
                return;

            if (_networkCharactersList.TryGetMyBrain(out var brain))
                _userId = brain.GetId();

            if (string.IsNullOrEmpty(_userId))
            {
                _logger.LogError($"{nameof(Handle)} userId is null or empty");
                return;
            }

            _presenter = presenter;

            if (!TryGetButtons(out Button beginButton, out Button stopButton))
                return;

            presenter.ResetButtons();

            beginButton.OnClickAsObservable()
                .Subscribe(_ => {
                    if (string.IsNullOrEmpty(_userId)) 
                        return;

                    TryStartRacingMode();
                })
                .AddTo(disposables);

            stopButton.OnClickAsObservable()
                .Subscribe(_ => Cancel())
                .AddTo(disposables);
        }

        public void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_beginClicked)
            {
                _analytics.LogNavigationStageGroupCancel(_placement, AnalyticsHandler.MultiStageRacing);
                _beginClicked = false;
            }

            _uiNavigator.Hide<WorldMatchMakingPresenter>();
            _presenter?.ResetButtons();

            if (_networkRpcView && !string.IsNullOrEmpty(_userId))
                _networkRpcView.ServerCancelSwitchingMode(_userId);
        }

        private async void TryStartRacingMode()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                if (!TryGetBeginButton(out Button beginButton))
                    return;

                beginButton.interactable = false;

                await _presenter.WaitSave(_cts.Token);

                if (!TryGetButtons(out beginButton, out Button stopButton))
                    return;

                if (!StartCountdownForEnterRacing())
                    return;

                stopButton.gameObject.SetActive(true);
                beginButton.gameObject.SetActive(false);

                _beginClicked = true;
                _analytics.LogNavigationStageGroupPrepared(_placement, AnalyticsHandler.MultiStageRacing);

                if (!_networkRpcView || string.IsNullOrEmpty(_userId))
                    return;

                _networkRpcView.ServerPrepareToSwitchMode(_userId);
            }
            catch (Exception)
            {
                // Ignore
            }
            finally
            {
                if (TryGetBeginButton(out Button beginButton))
                    beginButton.interactable = true;
            }
        }

        private bool StartCountdownForEnterRacing()
        {
            if (_disposables == null || _disposables.IsDisposed)
                return false;

            WorldMatchMakingPresenter worldMatchMakingPresenter = _uiNavigator.Resolve<WorldMatchMakingPresenter>();
            RacingCarPickMenuPresenter racingCarPickMenuPresenter = _uiNavigator.Resolve<RacingCarPickMenuPresenter>();
            ModeDescription modeMeta = _gameModesData.GetModeDescription(GameMode.Racing);

            if (worldMatchMakingPresenter?.Model == null ||
                racingCarPickMenuPresenter?.View == null ||
                !racingCarPickMenuPresenter.View.TimerParent ||
                modeMeta == null)
                return false;

            worldMatchMakingPresenter.Model.GameModeName = modeMeta.title;
            worldMatchMakingPresenter.Model.TimeRemaining = _timeRemaining;

            worldMatchMakingPresenter.OnTimeRemainingZeroEvent
                .Take(1)
                .Subscribe(_ =>
                {
                    _uiNavigator.Hide<WorldMatchMakingPresenter>();
                    _presenter?.ResetButtons();
                }).AddTo(_disposables);

            _uiNavigator
                .ShowAsync<WorldMatchMakingPresenter>(racingCarPickMenuPresenter.View.TimerParent)
                .Forget(CrashLogger.LogException);

            return true;
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