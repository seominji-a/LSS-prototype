using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace LSS_prototype.User_Page
{
    public class DefaultViewModel : INotifyPropertyChanged
    {
        // ────────────────────────────────────────
        // 범위 & 스텝 상수
        // ────────────────────────────────────────
        private const double ExposureMin = 0.2, ExposureMax = 1.0, ExposureStep = 0.1;
        private const double GainMin = 3.0, GainMax = 45.0, GainStep = 1.0;
        private const double GammaMin = 0.3, GammaMax = 1.0, GammaStep = 0.1;
        private const double FocusMin = 3545, FocusMax = 8310, FocusStep = 100;
        private const double IrisMin = 0, IrisMax = 656, IrisStep = 50;
        private const int ZoomMin = 1138, ZoomMax = 4669, ZoomStep = 100;

        // ────────────────────────────────────────
        // 커맨드
        // ────────────────────────────────────────
        public ICommand ExposureIncCommand { get; }
        public ICommand ExposureDecCommand { get; }
        public ICommand GainIncCommand { get; }
        public ICommand GainDecCommand { get; }
        public ICommand GammaIncCommand { get; }
        public ICommand GammaDecCommand { get; }
        public ICommand FocusIncCommand { get; }
        public ICommand FocusDecCommand { get; }
        public ICommand IrisIncCommand { get; }
        public ICommand IrisDecCommand { get; }
        public ICommand ZoomIncCommand { get; }
        public ICommand ZoomDecCommand { get; }
        public ICommand FilterOnCommand { get; }
        public ICommand FilterOffCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SaveCommand { get; }


        // 생성자
        public DefaultViewModel()
        {
            // '_' 는 "이 파라미터는 사용하지 않는다"는 의미의 관용적 표현 (discard)
            // RelayCommand는 Action<object>를 요구하므로 형식상 파라미터 자리를 채워줘야 하지만
            // +/- 커맨드는 전달받을 값이 없으므로 _ 로 명시적으로 표현하였음 ( 0227 박한용 ) 
            ExposureIncCommand = new RelayCommand(_ => ChangeExposure(+ExposureStep));
            ExposureDecCommand = new RelayCommand(_ => ChangeExposure(-ExposureStep));
            GainIncCommand = new RelayCommand(_ => ChangeGain(+GainStep));
            GainDecCommand = new RelayCommand(_ => ChangeGain(-GainStep));
            GammaIncCommand = new RelayCommand(_ => ChangeGamma(+GammaStep));
            GammaDecCommand = new RelayCommand(_ => ChangeGamma(-GammaStep));
            FocusIncCommand = new RelayCommand(_ => ChangeFocus(+FocusStep));
            FocusDecCommand = new RelayCommand(_ => ChangeFocus(-FocusStep));
            IrisIncCommand = new RelayCommand(_ => ChangeIris(+IrisStep));
            IrisDecCommand = new RelayCommand(_ => ChangeIris(-IrisStep));
            ZoomIncCommand = new RelayCommand(_ => ChangeZoom(+ZoomStep));
            ZoomDecCommand = new RelayCommand(_ => ChangeZoom(-ZoomStep));
            FilterOnCommand = new RelayCommand(_ => FilterValue = 1);
            FilterOffCommand = new RelayCommand(_ => FilterValue = 0);
            ResetCommand = new RelayCommand(_ => LoadDefaultSet(showMessage: true));
            SaveCommand = new RelayCommand(_ => ExecuteSave());

            LoadDefaultSet();
        }

        // DB 로드
        private void LoadDefaultSet(bool showMessage = false)
        {
            try
            {
                var db = new DB_Manager();
                var data = db.GetDefaultSet();

                ExposureValue = data.ExposureTime;
                GainValue = data.Gain;
                GammaValue = data.Gamma;
                FocusValue = data.Focus;
                IrisValue = data.Iris;
                ZoomValue = data.Zoom;
                FilterValue = data.Filter;

                if (showMessage)
                {
                    CustomMessageWindow.Show("리셋되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // DB 저장
        private void ExecuteSave()
        {
            try
            {
                var confirm = CustomMessageWindow.Show(
                    "기본값을 정말 변경하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();
                var data = new DefaultModel
                {
                    ExposureTime = ExposureValue,
                    Gain = GainValue,
                    Gamma = GammaValue,
                    Focus = FocusValue,
                    Iris = IrisValue,
                    Zoom = ZoomValue,
                    Filter = FilterValue
                };

                bool success = db.UpdateDefaultSet(data);

                if (success)
                {
                    CustomMessageWindow.Show("변경되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // 값 변경 헬퍼 
        private static double Clamp(double val, double min, double max) =>
            val < min ? min : val > max ? max : val;
        private static int Clamp(int val, int min, int max) =>
            val < min ? min : val > max ? max : val;

        private void ChangeExposure(double delta) =>
            ExposureValue = Math.Round(Clamp(ExposureValue + delta, ExposureMin, ExposureMax), 1);
        private void ChangeGain(double delta) =>
            GainValue = Math.Round(Clamp(GainValue + delta, GainMin, GainMax), 1);
        private void ChangeGamma(double delta) =>
            GammaValue = Math.Round(Clamp(GammaValue + delta, GammaMin, GammaMax), 1);
        private void ChangeFocus(double delta) =>
            FocusValue = Clamp(FocusValue + delta, FocusMin, FocusMax);
        private void ChangeIris(double delta) =>
            IrisValue = Clamp(IrisValue + delta, IrisMin, IrisMax);
        private void ChangeZoom(int delta) =>
            ZoomValue = Clamp(ZoomValue + delta, ZoomMin, ZoomMax);


        // 바인딩 프로퍼티
        private double _exposureValue;
        public double ExposureValue
        {
            get => _exposureValue;
            set { _exposureValue = value; OnPropertyChanged(); }
        }

        private double _gainValue;
        public double GainValue
        {
            get => _gainValue;
            set { _gainValue = value; OnPropertyChanged(); }
        }

        private double _gammaValue;
        public double GammaValue
        {
            get => _gammaValue;
            set { _gammaValue = value; OnPropertyChanged(); }
        }

        private double _focusValue;
        public double FocusValue
        {
            get => _focusValue;
            set { _focusValue = value; OnPropertyChanged(); }
        }

        private double _irisValue;
        public double IrisValue
        {
            get => _irisValue;
            set { _irisValue = value; OnPropertyChanged(); }
        }

        private int _zoomValue;
        public int ZoomValue
        {
            get => _zoomValue;
            set { _zoomValue = value; OnPropertyChanged(); }
        }

        private int _filterValue;
        public int FilterValue
        {
            get => _filterValue;
            set
            {
                _filterValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilterOnBackground));
                OnPropertyChanged(nameof(FilterOffBackground));
            }
        }

        // Filter 토글 배경색
        public SolidColorBrush FilterOnBackground =>
            FilterValue == 1
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

        public SolidColorBrush FilterOffBackground =>
            FilterValue == 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

   
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
