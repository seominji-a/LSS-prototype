// ================================================================
// 파일명 : ScanViewModel.cs
// 위치   : LSS prototype / Scan_Page / ScanViewModel.cs
//
// 이 파일의 역할
//   Scan 화면의 두뇌 역할을 해요.
//   화면(Scan.xaml) 과 카메라(CameraService) 사이를 연결해요.
//
//   쉽게 비유하면?
//   CameraService → 이미지 전달 → ScanViewModel → 화면에 표시
//
// ViewModel 이 하는 일
//   - CameraService 를 소유하고 카메라 연결 / 해제를 담당
//   - 카메라에서 새 프레임이 오면 PreviewSource 를 업데이트
//   - 화면(Scan.xaml) 이 PreviewSource 를 바인딩해서 자동으로 갱신됨
//   - 페이지 이동, 로그아웃, 종료 커맨드 처리
// ================================================================

//using LSS_prototype.Common_Module;      
using LSS_prototype.Common_Module;
using System;
using System.ComponentModel;            
using System.Runtime.CompilerServices; 
using System.Threading.Tasks;           
using System.Windows;                   
using System.Windows.Input;             
using System.Windows.Media.Imaging;     
using System.Windows.Threading;         

namespace LSS_prototype.Scan_Page
{
 
    // IDisposable 을 상속받는 이유?
    //   Scan.xaml.cs 의 Unloaded 이벤트에서 Dispose() 를 호출하면
    //   카메라까지 같이 정리
    // ================================================================
    public class ScanViewModel : INotifyPropertyChanged, IDisposable
    {
        // Scan 화면이 열릴 때 같이 생성되고, 닫힐 때 같이 해제
        private readonly CameraService _cameraService = new CameraService();
        private bool _disposed = false; // 찰나의 타이밍에 dispose를 하던 도중 마지막 프레임이 도착했을때, 에러방지 위해 flag 변수 사용 
        private int _frameCount = 0;
        private DateTime _lastTime = DateTime.Now;


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

            // 카메라에서 새 프레임이 오면 OnFrameArrived() 를 호출
            // += 문법 : "이 상황이 생기면 이 함수 불러줘" 라고 등록하는 것
            _cameraService.FrameArrived += OnFrameArrived;

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
                    if (success)
                        _cameraService.StartLiveView();
                }
                catch (Exception ex)
                {
                    OnCameraError($"카메라 스레드 오류: {ex.Message}");
                }
            });
        }



        // ================================================================
        // 프레임 도착 콜백
        // ================================================================
        /// <summary>
        /// CameraService 가 새 프레임을 처리하면 자동으로 호출
        /// 백그라운드 스레드에서 호출되므로 Dispatcher 로 UI 업데이트
        /// </summary>
        /// <param name="bitmap">처리 완료된 화면에 표시할 이미지</param>
        private void OnFrameArrived(WriteableBitmap bitmap)
        {

            if (_disposed) return;

            // FPS 측정
            _frameCount++;
            if ((DateTime.Now - _lastTime).TotalSeconds >= 1.0)
            {
                Console.WriteLine($"> FPS: {_frameCount}");
                _frameCount = 0;
                _lastTime = DateTime.Now;
            }

            Application.Current?.Dispatcher.BeginInvoke(
                new Action(() => PreviewSource = bitmap),
                DispatcherPriority.Render);
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
                // 상태 텍스트를 에러 내용으로 업데이트
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

            _cameraService.FrameArrived -= OnFrameArrived;
            _cameraService.ErrorOccurred -= OnCameraError;

            try { _cameraService.StopLiveView(); } catch { }
            _cameraService.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}