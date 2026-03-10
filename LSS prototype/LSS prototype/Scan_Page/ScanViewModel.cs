using LSS_prototype.Common_Module;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Lens_Module;
using LSS_prototype.User_Page;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LSS_prototype.Scan_Page {

    public class ScanViewModel : INotifyPropertyChanged, IDisposable
    {
        // Scan 화면이 열릴 때 같이 생성되고, 닫힐 때 같이 해제
        private readonly CameraService _cameraService = new CameraService();
        private bool _disposed = false; // 찰나의 타이밍에 dispose를 하던 도중 마지막 프레임이 도착했을때, 에러방지 위해 flag 변수 사용 


        // exe 바로 옆에 있는 테스트 영상 경로
        // 카메라가 없을 때 자동으로 이 영상을 재생
        private static readonly string TEST_VIDEO_PATH = Path.Combine(Common.executablePath, "sample.avi");


        // 카메라 라이브 화면을 담는 변수 ( xaml에 이미지 바인딩 역할 ) 
        private WriteableBitmap _previewSource;
        public WriteableBitmap PreviewSource
        {
            get => _previewSource;
            private set
            {
                _previewSource = value;
                OnPropertyChanged();
            }
        }

        private string _zoomText = $"{LensCtrl.Instance.zoomCurrentAddr}";
        public string ZoomText
        {
            get => _zoomText;
            private set { _zoomText = value; OnPropertyChanged(); }
        }


        private string _focusText = $"{LensCtrl.Instance.focusCurrentAddr}";
        public string FocusText
        {
            get => _focusText;
            private set { _focusText = value; OnPropertyChanged(); }
        }

        private string _gainText;
        public string GainText
        {
            get => _gainText;
            private set { _gainText = value; OnPropertyChanged(); }
        }

        private string _exposureText;
        public string ExposureText
        {
            get => _exposureText;
            private set { _exposureText = value; OnPropertyChanged(); }
        }

        private double _gammaValue;
        public double GammaValue
        {
            get => _gammaValue;
            private set { _gammaValue = value;
                _gammaValue = Math.Round(value, 2); 
                OnPropertyChanged(); }
            }

        private double _irisValue;
        public double IrisValue
        {
            get => _irisValue;
            private set { _irisValue = value; OnPropertyChanged(); }
        }

        //스캔 최초 설정값 Origin-> 버튼클릭 시 -> 토글형식으로 Gray, Red로 
        public string ColorMap { get; set; } = "Origin";

        private string _sharpness;
        public string Sharpness
        {
            get => _sharpness;
            private set { _sharpness = value; OnPropertyChanged(); }
        }

        private int _filterValue;
        public int FilterValue
        {
            get => _filterValue;
            private set
            {
                _filterValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilterOnBackground));
                OnPropertyChanged(nameof(FilterOffBackground));
            }
        }

        public ICommand NavigatePatientCommand { get; private set; }

        public ICommand LogoutCommand { get; }

        public ICommand ExitCommand { get; }
        public ICommand ColorMapCommand { get; }

        public ICommand ZoomIncCommand { get; }
        public ICommand ZoomDecCommand { get; }

        public ICommand FocusIncCommand { get; }
        public ICommand FocusDecCommand { get; }

        public ICommand AutoFocusCommand { get; }
        public ICommand GainIncCommand { get; }
        public ICommand GainDecCommand { get; }
        public ICommand ExposureIncCommand { get; }
        public ICommand ExposureDecCommand { get; }
        public ICommand GammaIncCommand { get; }
        public ICommand GammaDecCommand { get; }
        public ICommand IrisIncCommand { get; }
        public ICommand IrisDecCommand { get; }

        public ICommand ResetSettingCommand { get; }
        public ICommand FilterOnCommand { get; }
        public ICommand FilterOffCommand { get; }



        public ScanViewModel()
        {
            NavigatePatientCommand = new RelayCommand(NavigateToPatient);
            LogoutCommand = new RelayCommand(Common.ExecuteLogout);
            ExitCommand = new RelayCommand(Common.ExcuteExit);
            ColorMapCommand = new RelayCommand(ToggleColorMap);

            // 카메라에서 에러가 생기면 OnCameraError() 를 호출
            _cameraService.FrameArrived += OnFrameArrived;
            _cameraService.ErrorOccurred += OnCameraError;

            ConnectCamera();

            ZoomIncCommand = new RelayCommand(OnZoomInc);
            ZoomDecCommand = new RelayCommand(OnZoomDec);

            FocusIncCommand = new RelayCommand(OnFocusInc);
            FocusDecCommand = new RelayCommand(OnFocusDec);

            _cameraService.SharpnessUpdated += (val) => Sharpness = $"{val:F2}";

            AutoFocusCommand = new RelayCommand(OnAutoFocus);

            _cameraService.CameraDisconnected += OnCameraDisconnected;
            _cameraService.CameraReconnected += OnCameraReconnected;

            GainIncCommand = new RelayCommand(OnGainInc);
            GainDecCommand = new RelayCommand(OnGainDec);

            ExposureIncCommand = new RelayCommand(_ => OnExposureInc());
            ExposureDecCommand = new RelayCommand(_ => OnExposureDec());

            GammaIncCommand = new RelayCommand(_ => OnGammaInc());
            GammaDecCommand = new RelayCommand(_ => OnGammaDec());

            IrisIncCommand = new RelayCommand(_ => OnIrisInc());
            IrisDecCommand = new RelayCommand(_ => OnIrisDec());

            ResetSettingCommand = new RelayCommand(ResetValue);

            FilterOnCommand = new RelayCommand(_ => OnFilterOn());
            FilterOffCommand = new RelayCommand(_ => OnFilterOff());


        }

        private void ResetValue()
        {
            var confirm = CustomMessageWindow.Show(
                "기본 셋팅값으로 초기화하시겠습니까?",
                CustomMessageWindow.MessageBoxType.YesNo,
                0,
                CustomMessageWindow.MessageIconType.Info);

            if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

            DB_Manager db = new DB_Manager();
            DefaultModel data = db.GetDefaultSet();

            if (data == null) return;

            // 카메라 + 렌즈 실제 초기화
            _cameraService.InitializeCameraSettings(data);

            // UI 업데이트
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ExposureText = $"{data.ExposureTime / 1000000:F1}s";
                GainText = $"{data.Gain:F1} dB";
                GammaValue = data.Gamma;
                IrisValue = data.Iris;
            });

            CustomMessageWindow.Show("초기화 되었습니다.",
                CustomMessageWindow.MessageBoxType.AutoClose, 1,
                CustomMessageWindow.MessageIconType.Info);
        }
        // Filter 토글 배경색 (DefaultViewModel 이랑 동일하게)
        public SolidColorBrush FilterOnBackground =>
            FilterValue == 1
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

        public SolidColorBrush FilterOffBackground =>
            FilterValue == 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

        // 메서드
        private void OnFilterOn()
        {
            LensCtrl.Instance.OptFilterMove(1);
            FilterValue = 1;
        }

        private void OnFilterOff()
        {
            LensCtrl.Instance.OptFilterMove(0);
            FilterValue = 0;
        }

        // Exposure
        private void OnExposureInc()
        {
            _cameraService.ExposureInc();
            ExposureText = $"{_cameraService.ExposureCurrentRead() / 1000000:F1}s";
        }
        private void OnExposureDec()
        {
            _cameraService.ExposureDec();
            ExposureText = $"{_cameraService.ExposureCurrentRead() / 1000000:F1}s";
        }

        // Gain
        private void OnGainInc()
        {
            _cameraService.GainInc();
            GainText = $"{_cameraService.GainCurrentRead():F1} dB";
        }
        private void OnGainDec()
        {
            _cameraService.GainDec();
            GainText = $"{_cameraService.GainCurrentRead():F1} dB";
        }

        // Gamma
        private void OnGammaInc()
        {
            _cameraService.GammaInc();
            GammaValue = _cameraService.GammaCurrentRead();
        }
        private void OnGammaDec()
        {
            _cameraService.GammaDec();
            GammaValue = _cameraService.GammaCurrentRead();
        }

        // Iris
        private void OnIrisInc()
        {
            _cameraService.IrisInc();
            IrisValue = _cameraService.IrisCurrentRead();
        }
        private void OnIrisDec()
        {
            _cameraService.IrisDec();
            IrisValue = _cameraService.IrisCurrentRead();
        }

        private void OnCameraDisconnected()
        {
            Application.Current?.Dispatcher.Invoke(() =>
                _cameraService.StartTestVideo(TEST_VIDEO_PATH));
        }

        private void OnCameraReconnected()
        {
            Application.Current?.Dispatcher.Invoke(() =>
                Console.WriteLine("> 카메라 재연결 완료"));
        }

        private async void OnAutoFocus()
        {
            await _cameraService.AutoFocus();
        }


        private void OnZoomInc()
        {
            try
            {
                _cameraService.ZoomIn();
                ZoomText = $"{LensCtrl.Instance.zoomCurrentAddr}";
            }
            catch(Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void OnZoomDec()
        {
            try
            {
                _cameraService.ZoomOut();
                ZoomText = $"{LensCtrl.Instance.zoomCurrentAddr}";
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void OnFocusInc()
        {
            try
            {
                _cameraService.FocusIn();
                FocusText = $"{LensCtrl.Instance.focusCurrentAddr}";
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }


        private void OnFocusDec()
        {
            try
            {
                _cameraService.FocusOut();
                FocusText = $"{LensCtrl.Instance.focusCurrentAddr}";
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }



        private void OnFrameArrived(WriteableBitmap bitmap)
        {
            if (_disposed) return;
            Application.Current?.Dispatcher.Invoke(
                () => PreviewSource = bitmap,
                DispatcherPriority.Render);
        }



        // ================================================================
        // 컬러맵 
        // ================================================================
        private void ToggleColorMap()
        {
            string prev = _cameraService.ColorMap;

            if (_cameraService.ColorMap == "Origin") _cameraService.ColorMap = "Rainbow";
            else if (_cameraService.ColorMap == "Rainbow") _cameraService.ColorMap = "Invert";
            else _cameraService.ColorMap = "Origin";

            Console.WriteLine($"> 컬러맵 변경: {prev} → {_cameraService.ColorMap}");
        }

        // ================================================================
        // 카메라 연결
        // ================================================================
        /// CameraService.Connect() 는 무거운 작업 -> Task.Run 으로 백그라운드에서 실행
        private void ConnectCamera()
        {
            Task.Run(() =>
            {
                try
                {
                    bool success = _cameraService.Connect();

                    // 카메라가 없으면 테스트 영상으로 대체
                    if (!success)
                    {
                        Console.WriteLine("> 카메라 없음 → 테스트 영상 모드");
                        _cameraService.StartTestVideo(TEST_VIDEO_PATH);
                        return;
                    }

                    // ── DB 에서 기본값 읽어서 CameraService 로 전달 ──
                    DB_Manager db = new DB_Manager();
                    DefaultModel data = db.GetDefaultSet();

                    if (data != null)
                    {
                        _cameraService.InitializeCameraSettings(data);

                        Application.Current?.Dispatcher.Invoke(() =>
                            GainText = $"{data.Gain:F1} dB");

                        // 세팅 패널 초기값
                        ExposureText = $"{data.ExposureTime / 1000000:F1}s";
                        GainText = $"{data.Gain:F1} dB";
                        GammaValue = data.Gamma;
                        IrisValue = data.Iris;
                        FilterValue = data.Filter;
                    }
                    else
                    {
                        Console.WriteLine("> DB 기본값 없음 → 카메라 기본값 사용");
                    }

                    _cameraService.StartLiveView();
                    GainText = $"{_cameraService.GainCurrentRead():F1} dB";
                }
                catch (Exception ex)
                {
                    OnCameraError($"카메라 스레드 오류: {ex.Message}");
                }
            });
        }


        // ================================================================
        // 카메라 에러 콜백
        // ================================================================
        /// <summary>
        /// CameraService 에서 에러가 발생하면 자동으로 호출
        /// </summary>
        /// <param name="message">에러 내용 텍스트</param>
        private void OnCameraError(string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"오류 : {message}");
                Common.WriteSessionLog(message);
                CustomMessageWindow.Show(
                    message,
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
            });
        }

        private void NavigateToPatient()
        {
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());
        }


        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cameraService.ErrorOccurred -= OnCameraError;
            _cameraService.FrameArrived -= OnFrameArrived;

            _cameraService.CameraDisconnected -= OnCameraDisconnected;
            _cameraService.CameraReconnected -= OnCameraReconnected;

            try { _cameraService.StopLiveView(); } catch { }
            _cameraService.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


}