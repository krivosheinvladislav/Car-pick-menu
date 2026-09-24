using _Multiplayer._Program.Features.Profile.Client.UI;
using _Multiplayer._Program.Helpers;
using _Project._Program.Utils;
using MVP;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Multiplayer._Program.Features.RacingCarPickMenu
{
    public struct CarInfo
    {
        public string Name;
        public int Power;
        public int Speed;
        public string Boost;
        public int Health;
        public int Manageability;
        public WordIDs Class;
    }

    public sealed class RacingCarPickMenuView : BaseView
    {
        [field: SerializeField] public ProfileWidgetView ProfileWidgetView { get; private set; }

        [SerializeField] private TextMeshProUGUI powerValue;
        [SerializeField] private TextMeshProUGUI carClassValue;
        [SerializeField] private Image carClassBackground;
        [SerializeField] private TextMeshProUGUI speedValue;
        [SerializeField] private TextMeshProUGUI boostValue;
        [SerializeField] private TextMeshProUGUI healthValue;
        [SerializeField] private TextMeshProUGUI manageabilityValue;
        [SerializeField] private TextMeshProUGUI carName;
        [SerializeField] private Button begin;
        [SerializeField] private Button stop;
        [SerializeField] private Button exit;
        [SerializeField] private Transform itemViewParent;
        [SerializeField] private Transform timerParent;

        public Observable<Unit> OnExitClicked => exit.OnClickAsObservable();
        public Transform ItemViewParent => itemViewParent;
        public Transform TimerParent => timerParent;
        public Button StopButton => stop;
        public Button BeginButton => begin;
        
        public void SetCarInfo(CarInfo carInfo)
        {
            powerValue.text = carInfo.Power.ToString();
            carClassValue.text = Ln.Get(carInfo.Class);
            carClassBackground.color = Colors.GetClassColor(carInfo.Class);
            speedValue.text = carInfo.Speed.ToString();
            boostValue.text = carInfo.Boost;
            healthValue.text = carInfo.Health.ToString();
            manageabilityValue.text = carInfo.Manageability.ToString();
            carName.text = carInfo.Name;
        }
    }
}