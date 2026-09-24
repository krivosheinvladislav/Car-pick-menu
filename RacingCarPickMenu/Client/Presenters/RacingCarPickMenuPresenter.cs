using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _Multiplayer._Program.Features.Inventory.Client;
using _Multiplayer._Program.Features.Inventory.Client.Views;
using _Multiplayer._Program.Features.MatchMaking.Client;
using _Multiplayer._Program.Features.Profile.Backend;
using _Multiplayer._Program.Features.Profile.Client;
using _Multiplayer._Program.Features.Profile.Client.Interfaces;
using _Multiplayer._Program.Features.Profile.Client.Presenters;
using _Multiplayer._Program.Features.RacingCarPickMenu.Client.Interfaces;
using _Multiplayer._Program.Features.RacingCarPickMenu.Client.SupportScripts;
using _Multiplayer._Program.Helpers.Logger.Interfaces;
using _Multiplayer._Program.Modes.Lobby.Shared.Scriptables;
using _Multiplayer._Program.MST;
using _Multiplayer._Program.Services.Mode;
using _Project._Program.Handlers;
using AMFPC.Scriptables_Objects.Items.NonConsumables;
using Cysharp.Threading.Tasks;
using MasterServerToolkit.MasterServer;
using MVP;
using R3;
using VContainer;

namespace _Multiplayer._Program.Features.RacingCarPickMenu.Client.Presenters
{
    public sealed class RacingCarPickMenuPresenter : BasePresenter<RacingCarPickMenuView>, IDisposable
    {
        private const int SaveCarDelayMs = 1000;
        
        private readonly ClientMatchMakingService _clientMatchMakingService;
        private readonly ItemViewFactory _itemViewFactory;
        private readonly IUINavigator _uiNavigator;
        private readonly ProfileModuleClient _profileModuleClient;
        private readonly ClientProfileService _profileLoaderBehaviour;
        private readonly GameModesData _data;
        private readonly ModeService _modeService;
        private readonly IClientProfileFactory _clientProfileFactory;
        private readonly IObjectResolver _objectResolver;
        private readonly IAppLogger<RacingCarPickMenuPresenter> _logger;
        
        private CancellationTokenSource _saveCarCts;
        private ProfileWidgetPresenter _profileWidgetPresenter;
        private ICarPickStrategy _strategy;
        private Dictionary<int, (VehicleObject, ItemView)> _vehicles;
        private ItemView _selectedView;
        private bool _isSavingVehicle;

        public RacingCarPickMenuPresenter(
            ClientMatchMakingService clientMatchMakingService,
            ItemViewFactory itemViewFactory,
            IUINavigator uiNavigator,
            ProfileModuleClient profileModuleClient,
            ClientProfileService profileLoaderBehaviour,
            GameModesData data,
            ModeService modeService,
            IClientProfileFactory clientProfileFactory,
            IObjectResolver objectResolver, 
            IAppLogger<RacingCarPickMenuPresenter> logger
        )
        {
            _clientMatchMakingService = clientMatchMakingService;
            _itemViewFactory = itemViewFactory;
            _uiNavigator = uiNavigator;
            _profileModuleClient = profileModuleClient;
            _profileLoaderBehaviour = profileLoaderBehaviour;
            _data = data;
            _modeService = modeService;
            _clientProfileFactory = clientProfileFactory;
            _objectResolver = objectResolver;
            _logger = logger;
        }

        public void Dispose()
        {
            BeforeHide();
            _isSavingVehicle = false;
        }

        public override UniTask BeforeShow(CompositeDisposable disposables)
        {
            _profileWidgetPresenter?.Dispose();
            _profileWidgetPresenter = _clientProfileFactory.CreateLocalProfileWidget(View.ProfileWidgetView, ViewAddress);

            if (TryGetData(out CarPickShowData data))
            {
                _strategy = data.CarPickContext == RacingCarPickContext.InstantShow
                    ? new InstantCarPickStrategy(_clientMatchMakingService, Analytics, ViewAddress)
                    : new DelayedCarPickStrategy(_data, _uiNavigator, data.TimeRemaining, _objectResolver, Analytics, ViewAddress, _logger);

                _strategy.Handle(this, disposables);
            }

            _vehicles = _itemViewFactory.CreateCars(View.ItemViewParent, _profileLoaderBehaviour.Inventory);
            _profileLoaderBehaviour.OnProfileReady(OnProfileReady);

            View.OnExitClicked
                .Subscribe(_ => {
                    _strategy.Cancel();
                    _uiNavigator.Hide<RacingCarPickMenuPresenter>();
                })
                .AddTo(disposables);


            foreach (KeyValuePair<int, (VehicleObject, ItemView)> pair in _vehicles)
            {
                VehicleObject vehicle = pair.Value.Item1;
                ItemView view = pair.Value.Item2;

                view.itemButton
                    .OnClickAsObservable()
                    .Subscribe(_ => {
                        OnVehicleSelected(vehicle, view);
                        LogButtonClickEvent(AnalyticsHandler.ButtonCarPickSelectVehicle);
                    })
                    .AddTo(disposables);
            }

            return UniTask.CompletedTask;
        }

        public override void BeforeHide()
        {
            base.BeforeHide();

            _profileWidgetPresenter?.Dispose();
            _profileWidgetPresenter = null;

            _profileLoaderBehaviour.RemoveOnProfileReady(OnProfileReady);
            _saveCarCts?.Cancel();
            _saveCarCts = null;
        }

        public void ResetButtons()
        {
            if (View == null || !View.StopButton || !View.BeginButton)
                return;

            View.StopButton.gameObject.SetActive(false);
            View.BeginButton.gameObject.SetActive(true);
        }

        private void OnProfileReady(ObservableProfile observableProfile) => InitializeSelect();

        private void InitializeSelect()
        {
            int carId = _profileLoaderBehaviour.Profile.GetRacingPickedCar().Value;

            if (_vehicles.TryGetValue(carId, out (VehicleObject, ItemView) vehicle))
            {
                VehicleObject data = vehicle.Item1;
                ItemView view = _vehicles[carId].Item2;
                OnVehicleSelected(data, view);
            }
            else
            {
                if (_vehicles.Count == 0)
                    return;

                KeyValuePair<int, (VehicleObject, ItemView)> firstVehicle = _vehicles.First();

                VehicleObject data = firstVehicle.Value.Item1;
                ItemView view = firstVehicle.Value.Item2;

                if (!data || !view)
                    return;

                OnVehicleSelected(data, view);
            }
        }

        private void OnVehicleSelected(VehicleObject vehicle, ItemView clickedView)
        {
            if (_selectedView)
                _selectedView.SetSelected(false);

            _selectedView = clickedView;
            _selectedView.SetSelected(true);
            _saveCarCts?.Cancel();
            _saveCarCts = new CancellationTokenSource();

            SaveCarWithDelay(vehicle.id, _saveCarCts.Token).Forget();

            CarInfo info = new CarInfo
            {
                Name = vehicle.GetNameTranslation(),
                Power = vehicle.Power,
                Speed = vehicle.speed,
                Boost = vehicle.BoostStr,
                Health = vehicle.health,
                Manageability = vehicle.Manageability,
                Class = vehicle.Class,
            };

            View.SetCarInfo(info);
        }

        private async UniTaskVoid SaveCarWithDelay(int carId, CancellationToken ct)
        {
            try
            {
                _isSavingVehicle = true;

                await UniTask.Delay(SaveCarDelayMs, cancellationToken: ct, delayType: DelayType.Realtime);
                await _profileModuleClient.ClientProfileRacingPickedCar(carId, ct);
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception e)
            {
                _logger.LogError(e);
            }
            finally
            {
                _isSavingVehicle = false;
            }
        }

        public async UniTask WaitSave(CancellationToken ct = default)
        {
            while (_isSavingVehicle)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
    }
}