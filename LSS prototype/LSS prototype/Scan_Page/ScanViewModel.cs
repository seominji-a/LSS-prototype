


using LSS_prototype.Common_Module;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LSS_prototype.Scan_Page { 

    public class ScanViewModel : INotifyPropertyChanged, IDisposable
    {
        // Scan 화면이 열릴 때 같이 생성되고, 닫힐 때 같이 해제
        private readonly CameraService _cameraService = new CameraService();
        private bool _disposed = false; // 찰나의 타이밍에 dispose를 하던 도중 마지막 프레임이 도착했을때, 에러방지 위해 flag 변수 사용 
        private int _frameCount = 0;
        private DateTime _lastTime = DateTime.Now;

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

        public ICommand NavigatePatientCommand { get; private set; }

        // 로그아웃 버튼
        public ICommand LogoutCommand { get; }

        // 종료 버튼
        public ICommand ExitCommand { get; }


        public ScanViewModel()
        {
            NavigatePatientCommand = new RelayCommand(NavigateToPatient);
            LogoutCommand = new RelayCommand(Common.ExecuteLogout);
            ExitCommand = new RelayCommand(Common.ExcuteExit);

            // 카메라에서 에러가 생기면 OnCameraError() 를 호출
            _cameraService.ErrorOccurred += OnCameraError;

            ConnectCamera();
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

                    _cameraService.StartLiveView();
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
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
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

            try { _cameraService.StopLiveView(); } catch { }
            _cameraService.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


}