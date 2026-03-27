using FellowOakDicom;
using FellowOakDicom.Imaging;
using LSS_prototype.Auth;
using LSS_prototype.Common_Module;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Dicom_Module;
using LSS_prototype.ImageReview_Page;
using LSS_prototype.Lens_Module;
using LSS_prototype.Login_Page;
using LSS_prototype.Patient_Page;
using LSS_prototype.User_Page;
using LSS_prototype.VideoReview_Page;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;


namespace LSS_prototype.Scan_Page
{
    public class ScanViewModel : INotifyPropertyChanged, IDisposable
    {
        #region 필드

        // ── 카메라 서비스 ──
        private readonly CameraService _cameraService = new CameraService();
        private bool _disposed = false;
        private static readonly string TEST_VIDEO_PATH = Path.Combine(Common.executablePath, "sample.avi");
        private readonly ScanModel _img = new ScanModel();

        // ── 촬영 관련 ──
        private string _currentStudyId;
        private int _currentInstanceIndex = 0;
        private bool _isFrameReady = false;          // 프레임 준비 전 촬영 방지
        private bool _isBusy = false;

        // ── Dicom Record 녹화 관련 ──
        private VideoWriter _videoWriter;            // 영상기록 객체
        private string _aviSavePath;                 // AVI 최종 저장 경로
        private bool _isDicomRecording = false;      // 현재 녹화 중인지 여부
        private CancellationTokenSource _recordCts;  // 녹화 중지 신호
        private DateTime _recordStartTime;           // 녹화 시작 시간
        private Task _recordLoopTask;                // RecordLoop Task (중지 시 완료 대기용)
        private int _lastRecordingSecond = -1;       // UI 경과시간 업데이트 최적화용

        // ── AVI Only 녹화 관련 ──
        private VideoWriter _aviOnlyWriter;
        private string _aviOnlySavePath;
        private bool _isVideoRecording = false;
        private CancellationTokenSource _aviOnlyCts;
        private Task _aviOnlyLoopTask;
        private DateTime _aviOnlyStartTime;
        private int _lastAviOnlySecond = -1;

        // ── 공유 동영상 인덱스 ──
        // Dicom Record / AVI Only 둘 다 이 인덱스를 공유
        // VIDEO/ 폴더 기준으로 촬영 순서 보장
        private int _currentVideoIndex = 0;

        #endregion

        #region 바인딩 프로퍼티

        // ── 선택된 환자 ──
        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set { _selectedPatient = value; OnPropertyChanged(); }
        }

        // ── Dicom Record 상태 ──
        private bool _isDicomRecordingProp;
        public bool IsDicomRecording
        {
            get => _isDicomRecordingProp;
            set { _isDicomRecordingProp = value; OnPropertyChanged(); }
        }

        // ── Dicom Record 경과시간 ──
        private string _recordingTime = "00:00";
        public string RecordingTime
        {
            get => _recordingTime;
            set { _recordingTime = value; OnPropertyChanged(); }
        }

        // ── AVI Only 녹화 상태 ──
        private bool _isVideoRecordingProp;
        public bool IsVideoRecording
        {
            get => _isVideoRecordingProp;
            set { _isVideoRecordingProp = value; OnPropertyChanged(); }
        }

        // ── AVI Only 경과시간 ──
        private string _videoRecordingTime = "00:00";
        public string VideoRecordingTime
        {
            get => _videoRecordingTime;
            set { _videoRecordingTime = value; OnPropertyChanged(); }
        }

        // ── 카메라 미리보기 ──
        private WriteableBitmap _previewSource;
        public WriteableBitmap PreviewSource
        {
            get => _previewSource;
            private set { _previewSource = value; OnPropertyChanged(); }
        }

        // ── 렌즈 제어 ──
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

        // ── 카메라 설정값 ──
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
            private set { _gammaValue = Math.Round(value, 2); OnPropertyChanged(); }
        }

        private double _irisValue;
        public double IrisValue
        {
            get => _irisValue;
            private set { _irisValue = value; OnPropertyChanged(); }
        }

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

        // ── 카메라 상태 ──
        private bool _canImageScan;
        public bool CanImageScan
        {
            get => _canImageScan;
            set { _canImageScan = value; OnPropertyChanged(); }
        }

        private string _cameraStatus = "Camera Initializing...";
        public string CameraStatus
        {
            get => _cameraStatus;
            set
            {
                _cameraStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCameraInitializing));
            }
        }
        public bool IsCameraInitializing => CameraStatus == "Camera Initializing...";

        // ── 주사 시간 ──
        private string _injectionTime;
        public string InjectionTime
        {
            get => _injectionTime;
            set { _injectionTime = value; OnPropertyChanged(); }
        }

        // ── 썸네일 ──
        // 이미지 썸네일 (마지막 DCM 첫 프레임)
        private ImageSource _imageThumbnail;
        public ImageSource ImageThumbnail
        {
            get => _imageThumbnail;
            private set { _imageThumbnail = value; OnPropertyChanged(); }
        }

        // 영상 썸네일 (마지막 AVI 첫 프레임)
        private ImageSource _videoThumbnail;
        public ImageSource VideoThumbnail
        {
            get => _videoThumbnail;
            private set { _videoThumbnail = value; OnPropertyChanged(); }
        }

        // ── 팝업 메뉴 상태 ──
        private bool _isMenuOpen;
        private DateTime _menuLastClosed = DateTime.MinValue;
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set
            {
                if (_isMenuOpen && !value) _menuLastClosed = DateTime.UtcNow;
                _isMenuOpen = value;
                OnPropertyChanged();
            }
        }


        #endregion

        #region 커맨드

        public ICommand NavigatePatientCommand { get; private set; }
        public ICommand LockCommand { get; }
        public ICommand NavigateImageReviewCommand { get; private set; }
        public ICommand NavigateVideoReviewCommand { get; private set; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ToggleMenuCommand { get; }
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
        public ICommand ImageScanCommand { get; }
        public ICommand ImageCommentCommand { get; }
        public ICommand VideoCommentCommand { get; }
        public ICommand DicomRecordCommand { get; }
        public ICommand VideoRecordCommand { get; }  // AVI Only 녹화

        #endregion

        #region 생성자

        public ScanViewModel(PatientModel selectedPatient, string studyId = null)
        {
            SelectedPatient = selectedPatient;
            _currentStudyId = studyId;

            NavigatePatientCommand = new RelayCommand(_ => NavigateToPatient());
            LockCommand = new AsyncRelayCommand(async _ => await ExecuteLock());
            LogoutCommand = new AsyncRelayCommand(async _ => await ExecuteLogout());
            ExitCommand = new AsyncRelayCommand(async _ => await ExecuteExit());
            ToggleMenuCommand = new RelayCommand(_ => ToggleMenu());

            NavigateImageReviewCommand = new RelayCommand(_ => NavigateToImageReview());
            NavigateVideoReviewCommand = new RelayCommand(_ => NavigateToVideoReview());

            ColorMapCommand = new RelayCommand(_ => ToggleColorMap());

            _cameraService.FrameArrived += OnFrameArrived;
            _cameraService.ErrorOccurred += OnCameraError;
            _cameraService.SharpnessUpdated += (val) => Sharpness = $"{val:F2}";
            _cameraService.CameraDisconnected += OnCameraDisconnected;
            _cameraService.CameraReconnected += OnCameraReconnected;

            // 초반에는 프레임 도착 전이므로 이미지 스캔 가능 여부를 false로 고정
            CanImageScan = false;

            // 프레임 도착 전 카메라 준비 상태 사용자에게 확인 목적
            CameraStatus = "Camera Initializing...";

            //ConnectCamera();

            ZoomIncCommand = new RelayCommand(async _ => await OnZoomInc());
            ZoomDecCommand = new RelayCommand(async _ => await OnZoomDec());
            FocusIncCommand = new RelayCommand(async _ => await OnFocusInc());
            FocusDecCommand = new RelayCommand(async _ => await OnFocusDec());
            AutoFocusCommand = new RelayCommand(async _ => await _cameraService.AutoFocus());
            GainIncCommand = new RelayCommand(async _ => await OnGainInc());
            GainDecCommand = new RelayCommand(async _ => await OnGainDec());
            ExposureIncCommand = new RelayCommand(async _ => await OnExposureInc());
            ExposureDecCommand = new RelayCommand(async _ => await OnExposureDec());
            GammaIncCommand = new RelayCommand(async _ => await OnGammaInc());
            GammaDecCommand = new RelayCommand(async _ => await OnGammaDec());
            IrisIncCommand = new RelayCommand(async _ => await OnIrisInc());
            IrisDecCommand = new RelayCommand(async _ => await OnIrisDec());
            ResetSettingCommand = new RelayCommand(async _ => await ResetValue());
            FilterOnCommand = new RelayCommand(async _ => await OnFilterOn());
            FilterOffCommand = new RelayCommand(async _ => await OnFilterOff());
            ImageScanCommand = new RelayCommand(async _ => await CaptureAndSaveDicomAsync());
            ImageCommentCommand = new RelayCommand(async _ => await OpenImageComment());
            VideoCommentCommand = new RelayCommand(async _ => await OpenVideoComment());
            DicomRecordCommand = new RelayCommand(async _ => await ToggleDicomRecord());
            VideoRecordCommand = new RelayCommand(async _ => await ToggleVideoRecord());

            // 기존 썸네일 로드
            _ = RefreshThumbnailsAsync();

        }


        public async Task InitializeAsync()
        {
            await _cameraService.InitializeAsync(); // 렌즈 초기화 완료 보장
            await ConnectCamera();                        // 그 다음 카메라 연결
        }
        #endregion

        #region 공통 유효성 검사

        // ═══════════════════════════════════════════
        //  촬영 전 유효성 검사
        //  환자 선택 여부, 카메라 상태, 프레임 준비 여부 확인
        //  문제 있으면 false 반환 + 팝업 표시
        // ═══════════════════════════════════════════
        private async Task<bool> CaptureValidation()
        {
            if (SelectedPatient == null)
            {
                await CustomMessageWindow.ShowAsync("환자를 먼저 선택해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return false;
            }

            if (CameraStatus == "Camera Disconnected")
            {
                await CustomMessageWindow.ShowAsync("카메라가 연결되어 있지 않습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                return false;
            }

            if (!_isFrameReady)
            {
                await CustomMessageWindow.ShowAsync("카메라 영상이 \n 아직 준비되지 않았습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                return false;
            }

            return true;
        }

        #endregion

        #region 이미지 저장

        // ═══════════════════════════════════════════
        //  이미지 캡처 및 DICOM 저장
        //  1) 유효성 검사 (CaptureValidation)
        //  2) StudyID / instanceIndex 확정 (EnsureStudyReady)
        //  3) 프레임 캡처 → DICOM 변환 → 저장
        // ═══════════════════════════════════════════
        private async Task CaptureAndSaveDicomAsync()
        {
            // 처리 중이면 연타 무시
            if (_isBusy) return;
            System.Drawing.Bitmap bitmap = null;
            Mat frame = null;

            try
            {
                _isBusy = true;

                // ① 유효성 검사
                if (!await CaptureValidation()) return;

                // ② 프레임 캡처
                frame = _cameraService.GetCurrentFrame();
                if (frame == null || frame.Empty())
                {
                    await CustomMessageWindow.ShowAsync("촬영할 이미지가 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                bitmap = BitmapConverter.ToBitmap(frame);

                string date = DateTime.Now.ToString("yyyyMMdd");
                string time = DateTime.Now.ToString("HHmmss");

                string accessionNumber = (SelectedPatient.Dataset != null)
                    ? SelectedPatient.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "")
                    : "";

                double exposure = await _cameraService.ExposureCurrentRead();
                double gain = await _cameraService.GainCurrentRead();
                double gamma = await _cameraService.GammaCurrentRead();

                var db = new DB_Manager();
                var setting = db.GetPacsSet();
                string hospName = setting?.HospitalName ?? "";

                // ③ StudyID / instanceIndex 확정
                await EnsureStudyReady();

                // ④ instanceIndex 증가
                _currentInstanceIndex++;
                int instanceIndex = _currentInstanceIndex;

                // ⑤ DicomManager 생성
                // EMR 환자면 기존 Dataset 유지, LOCAL 환자면 새로 생성
                DicomManager dm = (SelectedPatient.Dataset == null)
                    ? new DicomManager(SelectedPatient.PatientId.ToString(), "00000001")
                    : new DicomManager(SelectedPatient.PatientId.ToString(), "00000001", SelectedPatient.Dataset);

                dm.SetPatient(
                    SelectedPatient.PatientCode.ToString(),
                    SelectedPatient.PatientName,
                    SelectedPatient.BirthDate.ToString("yyyyMMdd"),
                    SelectedPatient.Sex,
                    CalculateAge(SelectedPatient.BirthDate).ToString()
                );

                dm.SetStudy(_currentStudyId, accessionNumber, date, time, "", hospName, "");
                dm.SetSeries("1", "", date, time);
                dm.SetContent("1", date, time, instanceIndex.ToString());
                dm.SetPrivateDataElement(exposure, gain, gamma);

                // ⑥ 경로 생성 및 저장
                // DICOM/박한용_2634/20250313/202503130001/Image/박한용_2634_202503130001_1.dcm
                string path = GenerateImageSavePath(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString(),
                    _currentStudyId,
                    instanceIndex
                );

                await dm.SaveImageFile(path, bitmap);

                await UpdatePatientAfterScan();

                await CustomMessageWindow.ShowAsync("촬영 및 저장이 완료되었습니다.",
                    CustomMessageWindow.MessageBoxType.Ok, 1,
                    CustomMessageWindow.MessageIconType.Info);

                _ = RefreshThumbnailsAsync();   // 썸네일 업데이트 
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
            finally
            {
                // 사용한 리소스 반드시 해제

                frame?.Dispose();
                bitmap?.Dispose();
                _isBusy = false; //   잠금 해제 
            }
        }

        // ── 이미지 DCM 저장 경로 생성 ──
        // DICOM/박한용_2634/20250313/202503130001/Image/박한용_2634_202503130001_1.dcm
        private string GenerateImageSavePath(string name, string code, string studyID, int instanceIndex)
        {
            string patientFolderName = $"{name}_{code}";
            string studyDateFolder = studyID.Substring(0, 8);
            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string imageDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyID, "Image");
            Directory.CreateDirectory(imageDir);
            string fileName = $"{patientFolderName}_{studyID}_{instanceIndex}.dcm";
            return Path.Combine(imageDir, fileName);
        }

        #endregion

        #region 동영상 저장 (AVI Only)

        // ═══════════════════════════════════════════
        //  AVI Only 녹화 토글
        //  Dicom Record 중이면 경고 후 return
        //  녹화 중이면 → 중지
        //  녹화 중 아니면 → 유효성 검사 후 시작
        // ═══════════════════════════════════════════
        private async Task ToggleVideoRecord()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                // Dicom Record 촬영 중이면 경고
                if (_isDicomRecording)
                {
                    await CustomMessageWindow.ShowAsync("DICOM 영상 촬영 중...", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }
                
                if (_isVideoRecording)
                    await StopVideoRecord();
                else
                {
                    if (!await CaptureValidation()) return;
                    await StartVideoRecord();
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            finally { _isBusy = false; }
        }

        // ═══════════════════════════════════════════
        //  AVI Only 녹화 시작
        //  ① StudyID 확정 (GenerateAviOnlyPath 에서 _currentStudyId 사용하므로 먼저)
        //  ② 프레임 확인
        //  ③ VideoWriter 초기화 성공 확인 후 VideoIndex 증가
        //  ④ 녹화 상태 ON
        //  ⑤ AviOnlyRecordLoop Task 시작
        //  ⑥ 30초 자동 중지 타이머 (테스트 차 30초, 추후 15분으로 변경 예정)
        // ═══════════════════════════════════════════
        private async Task StartVideoRecord()
        {
            try
            {
                // ① StudyID 확정 먼저
                await EnsureStudyReady();

                // ② 프레임 확인
                var frame = _cameraService.GetCurrentFrame();
                if (frame == null || frame.Empty())
                {
                    await CustomMessageWindow.ShowAsync("카메라 영상이 준비되지 않았습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                double fps = _cameraService.GetCurrentFps();

                // ③ VideoWriter 초기화 먼저 확인
                // VideoWriter 실패 시 VideoIndex 증가하면 안 되므로 성공 확인 후 증가
                string tempAviPath = GenerateAviOnlyPath(_currentVideoIndex + 1);

                _aviOnlyWriter = new VideoWriter(
                    tempAviPath,
                    FourCC.MJPG,
                    fps,
                    new OpenCvSharp.Size(frame.Width, frame.Height)
                );

                if (!_aviOnlyWriter.IsOpened())
                {
                    _aviOnlyWriter?.Dispose();
                    _aviOnlyWriter = null;
                    frame.Dispose();
                    await CustomMessageWindow.ShowAsync("녹화를 시작할 수 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // VideoWriter 성공 확인 후 VideoIndex 증가
                _currentVideoIndex++;
                _aviOnlySavePath = tempAviPath;

                // ④ 녹화 상태 ON
                _isVideoRecording = true;
                IsVideoRecording = true;
                _aviOnlyStartTime = DateTime.Now;
                _lastAviOnlySecond = -1;
                _aviOnlyCts = new CancellationTokenSource();

                // ⑤ AviOnlyRecordLoop Task 시작
                _aviOnlyLoopTask = Task.Run(() => AviOnlyRecordLoop(_aviOnlyCts.Token, fps));

                // ⑥ 자동 중지 타이머 (테스트 차 30초, 추후 15분으로 변경 예정)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), _aviOnlyCts.Token);

                        // 팝업 전에 영상 먼저 중지
                        _aviOnlyCts?.Cancel();
                        if (_aviOnlyLoopTask != null)
                            await Task.WhenAny(_aviOnlyLoopTask, Task.Delay(1000));

                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            if (!_isVideoRecording) return; // 수동 중지된 경우 처리 안 함

                            _isVideoRecording = false;
                            IsVideoRecording = false;
                            VideoRecordingTime = "00:00";

                            _aviOnlyWriter?.Release(); // avi 파일을 생성하는 제일중요한코드
                            _aviOnlyWriter?.Dispose();
                            _aviOnlyWriter = null;

                            await CustomMessageWindow.ShowAsync("NORMAL VIDEO는 최대 30초 녹화\n녹화를 중지합니다.",
                                CustomMessageWindow.MessageBoxType.Ok, 0,
                                CustomMessageWindow.MessageIconType.Info);

                            if (string.IsNullOrEmpty(_aviOnlySavePath) || !File.Exists(_aviOnlySavePath))
                            {
                                _currentVideoIndex--;
                                await CustomMessageWindow.ShowAsync("저장된 영상 파일이 없습니다.",
                                    CustomMessageWindow.MessageBoxType.Ok, 2,
                                    CustomMessageWindow.MessageIconType.Warning);
                                return;
                            }
                            await CustomMessageWindow.ShowAsync("동영상 저장 완료.( NORMAL VIDEO )",
                                CustomMessageWindow.MessageBoxType.Ok, 2,
                                CustomMessageWindow.MessageIconType.Info);
                            _ = RefreshThumbnailsAsync();
                        });
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                });

                frame.Dispose();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  AVI Only 프레임 캡처 루프
        //  DicomRecord 의 RecordLoop 와 동일한 구조
        //  목표 간격 - 처리시간 = 남은 시간만 Sleep → 정확한 fps 유지
        // ═══════════════════════════════════════════
        private async void AviOnlyRecordLoop(CancellationToken ct, double fps)
        {
            var targetInterval = TimeSpan.FromMilliseconds(1000.0 / fps);

            while (!ct.IsCancellationRequested)
            {
                var frameStart = DateTime.Now;

                try
                {
                    var frame = _cameraService.GetCurrentFrame();
                    if (frame != null && !frame.Empty())
                    {
                        _aviOnlyWriter?.Write(frame);
                        frame.Dispose();
                    }

                    // 경과시간 UI 업데이트 - 1초마다만
                    var elapsed = DateTime.Now - _aviOnlyStartTime;
                    if (elapsed.Seconds != _lastAviOnlySecond)
                    {
                        _lastAviOnlySecond = elapsed.Seconds;
                        Application.Current?.Dispatcher.Invoke(() =>
                            VideoRecordingTime = elapsed.ToString(@"mm\:ss"));
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { await Common.WriteLog(ex); break; }

                var frameElapsed = DateTime.Now - frameStart;
                var remaining = targetInterval - frameElapsed;
                if (remaining > TimeSpan.Zero)
                    Thread.Sleep(remaining);
            }
        }

        // ═══════════════════════════════════════════
        //  AVI Only 녹화 중지 + 파일 저장
        //  ① 중지 신호
        //  ② AviOnlyRecordLoop 완전 종료 대기
        //  ③ VideoWriter 닫기 (Release 필수 - 안하면 AVI 파일 손상)
        //  ④ 파일 존재 확인
        // ═══════════════════════════════════════════
        private async Task StopVideoRecord()
        {
            if (!_isVideoRecording) return;

            try
            {
                // ① 중지 신호
                _aviOnlyCts?.Cancel();
                _isVideoRecording = false;
                IsVideoRecording = false;
                VideoRecordingTime = "00:00";

                // ② AviOnlyRecordLoop 완전 종료 대기
                // Cancel() 은 신호만 보낼 뿐 즉시 종료가 아님
                // RecordLoop 끝나거나 최대 1초 대기
                if (_aviOnlyLoopTask != null)
                {
                    await Task.WhenAny(_aviOnlyLoopTask, Task.Delay(1000));
                    _aviOnlyLoopTask = null;
                }

                // ③ VideoWriter 닫기

                _aviOnlyWriter?.Release();
                _aviOnlyWriter?.Dispose();
                _aviOnlyWriter = null;

                // ④ 파일 존재 확인
                // 파일이 없으면 VideoIndex 롤백 (인덱스 낭비 방지)
                if (string.IsNullOrEmpty(_aviOnlySavePath) || !File.Exists(_aviOnlySavePath))
                {
                    _currentVideoIndex--;
                    await CustomMessageWindow.ShowAsync("저장된 영상 파일이 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                await CustomMessageWindow.ShowAsync("동영상 저장 완료.( NORMAL VIDEO )", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Info);

                _ = RefreshThumbnailsAsync();  // 썸네일 작업 
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync($"영상 저장 실패: {ex.Message}",
                    CustomMessageWindow.MessageBoxType.Ok, 3,
                    CustomMessageWindow.MessageIconType.Warning);
            }
        }

        // ── AVI Only 저장 경로 생성 ──
        // VIDEO/박한용_2634/20250313/202503130001/박한용_2634_202503130001_1_Avi.avi
        private string GenerateAviOnlyPath(int index)
        {
            string patientFolderName = $"{SelectedPatient.PatientName}_{SelectedPatient.PatientCode}";
            string studyDateFolder = _currentStudyId.Substring(0, 8);
            string rootDir = Path.Combine(Common.executablePath, "VIDEO");
            string videoDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, _currentStudyId);
            Directory.CreateDirectory(videoDir);
            string fileName = $"{patientFolderName}_{_currentStudyId}_{index}_Avi.avi";
            return Path.Combine(videoDir, fileName);
        }

        #endregion

        #region 동영상 저장 (Dicom Record)

        // ═══════════════════════════════════════════
        //  Dicom Record 녹화 토글
        //  AVI Only 녹화 중이면 경고 후 return
        //  녹화 중이면 → 중지
        //  녹화 중 아니면 → 유효성 검사 후 시작
        // ═══════════════════════════════════════════
        private async Task ToggleDicomRecord()
        {
            if (_isBusy) return;
            _isBusy = true; // 
            try
            {
                // AVI Only 촬영 중이면 경고
                if (_isVideoRecording)
                {
                    await CustomMessageWindow.ShowAsync("AVI 영상 촬영 중...", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }
                
                if (_isDicomRecording)
                    await StopDicomRecord();
                else
                {
                    if (!await CaptureValidation()) return;
                    await StartDicomRecord();
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            finally { _isBusy = false; }
        }

        // ═══════════════════════════════════════════
        //  Dicom Record 녹화 시작
        //  ① 현재 프레임 확인
        //  ② StudyID 확정
        //  ③ VideoWriter 초기화 성공 확인 후 VideoIndex 증가
        //  ④ 녹화 상태 ON
        //  ⑤ RecordLoop Task 시작
        //  ⑥ 1분 자동 중지 타이머
        // ═══════════════════════════════════════════
        private async Task StartDicomRecord()
        {
            try
            {
                // ① 현재 프레임 확인
                var frame = _cameraService.GetCurrentFrame();
                if (frame == null || frame.Empty())
                {
                    await CustomMessageWindow.ShowAsync("카메라 영상이 준비되지 않았습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // ② StudyID 확정
                await EnsureStudyReady();

                // ③ VideoWriter 초기화 먼저 확인
                // VideoWriter 실패 시 VideoIndex 증가하면 안 되므로 성공 확인 후 증가
                double fps = _cameraService.GetCurrentFps();
                string tempAviPath = GenerateDicomAviPath(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString(),
                    _currentStudyId,
                    _currentVideoIndex + 1
                );

                _videoWriter = new VideoWriter(
                    tempAviPath,
                    FourCC.MJPG,
                    fps,
                    new OpenCvSharp.Size(frame.Width, frame.Height)
                );

                if (!_videoWriter.IsOpened())
                {
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                    frame.Dispose();
                    await CustomMessageWindow.ShowAsync("녹화를 시작할 수 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // VideoWriter 성공 확인 후 VideoIndex 증가
                _currentVideoIndex++;
                _aviSavePath = tempAviPath;

                // ④ 녹화 상태 ON
                _isDicomRecording = true;
                IsDicomRecording = true;
                _recordStartTime = DateTime.Now;
                _lastRecordingSecond = -1;
                _recordCts = new CancellationTokenSource();

                // ⑤ RecordLoop Task 시작
                _recordLoopTask = Task.Run(() => RecordLoop(_recordCts.Token, fps));

                // ⑥ 1분 자동 중지 타이머
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), _recordCts.Token);

                        // 팝업 전에 루프 먼저 중지 (실제 캡처 중단)
                        _recordCts?.Cancel();
                        if (_recordLoopTask != null)
                            await Task.WhenAny(_recordLoopTask, Task.Delay(1000));

                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            if (!_isDicomRecording) return; // 수동 중지된 경우 처리 안 함

                            IsDicomRecording = false;

                            await CustomMessageWindow.ShowAsync("DICOM VIDEO는 최대 10초 녹화\n녹화를 중지합니다.",
                                CustomMessageWindow.MessageBoxType.Ok, 0,
                                CustomMessageWindow.MessageIconType.Info);

                            await StopDicomRecord();
                        });
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                });

                frame.Dispose();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  Dicom Record 프레임 캡처 루프
        //  Cancel 신호 올 때까지 계속 실행
        //
        //  핵심 아이디어:
        //  프레임 처리 시간이 매번 다르기 때문에
        //  무조건 33ms Sleep 하면 실제 fps가 낮아짐
        //  해결: 목표간격 - 처리시간 = 남은 시간만 Sleep → 정확한 fps 유지
        // ═══════════════════════════════════════════
        private async void RecordLoop(CancellationToken ct, double fps)
        {
            var targetInterval = TimeSpan.FromMilliseconds(1000.0 / fps);

            while (!ct.IsCancellationRequested)
            {
                var frameStart = DateTime.Now;

                try
                {
                    var frame = _cameraService.GetCurrentFrame();
                    if (frame != null && !frame.Empty())
                    {
                        _videoWriter?.Write(frame);
                        frame.Dispose();
                    }

                    // 경과시간 UI 업데이트 - 1초마다만 Dispatcher 호출 (UI 스레드 부하 방지)
                    var elapsed = DateTime.Now - _recordStartTime;
                    if (elapsed.Seconds != _lastRecordingSecond)
                    {
                        _lastRecordingSecond = elapsed.Seconds;
                        Application.Current?.Dispatcher.Invoke(() =>
                            RecordingTime = elapsed.ToString(@"mm\:ss"));
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { await Common.WriteLog(ex); break; }

                var frameElapsed = DateTime.Now - frameStart;
                var remaining = targetInterval - frameElapsed;
                if (remaining > TimeSpan.Zero)
                    Thread.Sleep(remaining);
            }
        }

        // ═══════════════════════════════════════════
        //  Dicom Record 녹화 중지 + AVI 저장 + DICOM 변환
        //  ① 녹화 중지 신호
        //  ② RecordLoop 완전 종료 대기
        //  ③ VideoWriter 닫기 (Release 필수 - 안하면 AVI 파일 손상)
        //  ④ AVI 파일 존재 확인
        //  ⑤ AVI → DICOM 변환 저장
        // ═══════════════════════════════════════════
        private async Task StopDicomRecord()
        {
            if (!_isDicomRecording) return;

            try
            {
                // ① 녹화 중지 신호
                // RecordLoop while 탈출 + 1분 타이머 취소
                _recordCts?.Cancel();
                _isDicomRecording = false;
                IsDicomRecording = false;
                RecordingTime = "00:00";

                // ② RecordLoop 완전 종료 대기
                // Cancel() 은 신호만 보낼 뿐 즉시 종료가 아님
                // RecordLoop 끝나거나 최대 1초 대기
                if (_recordLoopTask != null)
                {
                    await Task.WhenAny(_recordLoopTask, Task.Delay(1000));
                    _recordLoopTask = null;
                }

                // ③ VideoWriter 닫기
                // Release() 필수! 안 하면 AVI 파일 손상 → DICOM 변환 실패
                _videoWriter?.Release();
                _videoWriter?.Dispose();
                _videoWriter = null;

                // ④ AVI 파일 존재 확인
                // 파일이 없으면 VideoIndex 롤백 (인덱스 낭비 방지)
                if (string.IsNullOrEmpty(_aviSavePath) || !File.Exists(_aviSavePath))
                {
                    _currentVideoIndex--;
                    await CustomMessageWindow.ShowAsync("저장된 영상 파일이 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // ⑤ DICOM 변환 시작 (로딩창 표시)
                LoadingWindow.Begin("DICOM 변환 중...");

                await Task.Run(async () =>
                {
                    string date = DateTime.Now.ToString("yyyyMMdd");
                    string time = DateTime.Now.ToString("HHmmss");

                    var db = new DB_Manager();
                    var setting = db.GetPacsSet();
                    string hospName = setting?.HospitalName ?? "";

                    string accessionNumber = (SelectedPatient.Dataset != null)
                        ? SelectedPatient.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "")
                        : "";

                    DicomManager dm = (SelectedPatient.Dataset == null)
                        ? new DicomManager(SelectedPatient.PatientId.ToString(), "00000001")
                        : new DicomManager(SelectedPatient.PatientId.ToString(), "00000001", SelectedPatient.Dataset);

                    dm.SetPatient(
                        SelectedPatient.PatientCode.ToString(),
                        SelectedPatient.PatientName,
                        SelectedPatient.BirthDate.ToString("yyyyMMdd"),
                        SelectedPatient.Sex,
                        CalculateAge(SelectedPatient.BirthDate).ToString()
                    );

                    dm.SetStudy(_currentStudyId, accessionNumber, date, time, "", hospName, "");
                    dm.SetSeries("1", "", date, time);
                    dm.SetContentVideo("1", date, time);

                    // DCM 저장 경로 생성
                    // DICOM/박한용_2634/20250313/202503130001/Video/박한용_2634_202503130001_1_Dicom.dcm
                    string dcmPath = GenerateDicomVideoPath(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        _currentStudyId,
                        _currentVideoIndex
                    );

                    // AVI → DICOM 변환 저장
                    dm.SaveVideoFile(dcmPath, _aviSavePath);
                    await UpdatePatientAfterScan();
                });

                LoadingWindow.End();

                await CustomMessageWindow.ShowAsync("동영상 저장 완료.( DICOM VIDEO )",
                    CustomMessageWindow.MessageBoxType.Ok, 1,
                    CustomMessageWindow.MessageIconType.Info);

                _ = RefreshThumbnailsAsync();   // 썸네일 업데이트 
            }
            catch (Exception ex)
            {
                LoadingWindow.End();
                await Common.WriteLog(ex);

                // AVI + DCM 둘 중 하나라도 실패하면 둘 다 삭제
                // → 불완전한 쌍 파일이 남아있으면 VideoComment에서 DCM 없는
                //   DICOM VIDEO 파일이 목록에 남는 불일치 상태가 생기기 때문
                if (!string.IsNullOrEmpty(_aviSavePath) && File.Exists(_aviSavePath))
                    File.Delete(_aviSavePath);

                string dcmPath = GenerateDicomVideoPath(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString(),
                    _currentStudyId,
                    _currentVideoIndex);

                if (File.Exists(dcmPath))
                    File.Delete(dcmPath);

                // VideoIndex 롤백 (실패했으므로 인덱스 낭비 방지)
                _currentVideoIndex--;

                await CustomMessageWindow.ShowAsync(
                    "녹화에 실패하였습니다.\n다시 녹화해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            finally
            {
                if (_videoWriter != null)
                {
                    _videoWriter.Release(); // AVI 파일 헤더를 닫고 저장 완료
                    _videoWriter.Dispose();
                    _videoWriter = null;
                }
                RecordingTime = "00:00";

            }
        }

        // ── Dicom Record AVI 저장 경로 생성 ──
        // VIDEO/박한용_2634/20250313/202503130001/박한용_2634_202503130001_1_Dicom.avi
        private string GenerateDicomAviPath(string name, string code, string studyID, int videoIndex)
        {
            string patientFolderName = $"{name}_{code}";
            string studyDateFolder = studyID.Substring(0, 8);
            string rootDir = Path.Combine(Common.executablePath, "VIDEO");
            string videoDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyID);
            Directory.CreateDirectory(videoDir);
            string fileName = $"{patientFolderName}_{studyID}_{videoIndex}_Dicom.avi";
            return Path.Combine(videoDir, fileName);
        }

        // ── Dicom Record DCM 저장 경로 생성 ──
        // DICOM/박한용_2634/20250313/202503130001/Video/박한용_2634_202503130001_1_Dicom.dcm
        private string GenerateDicomVideoPath(string name, string code, string studyID, int videoIndex)
        {
            string patientFolderName = $"{name}_{code}";
            string studyDateFolder = studyID.Substring(0, 8);
            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string videoDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyID, "Video");
            Directory.CreateDirectory(videoDir);
            string fileName = $"{patientFolderName}_{studyID}_{videoIndex}_Dicom.dcm";
            return Path.Combine(videoDir, fileName);
        }

        #endregion

        #region StudyID 관리

        // ═══════════════════════════════════════════
        //  StudyID / Index 결정
        //  _currentStudyId 없으면 → ResolveStudyId() 로 확정
        //  _currentStudyId 있지만 instanceIndex 가 0 이면
        //  → Comment/Review 에서 돌아온 경우이므로 마지막 인덱스 복원
        // ═══════════════════════════════════════════
        private async Task EnsureStudyReady()
        {
            // ═══════════════════════════════════════════
            //  StudyID 가 없는 경우
            //  → 사용자가 오늘 처음 촬영 버튼을 누른 상황
            //  → 오늘 촬영 이력을 확인해서 StudyID 를 결정
            // ═══════════════════════════════════════════
            if (string.IsNullOrEmpty(_currentStudyId))
            {
                // 오늘 촬영 이력 기준으로 StudyID 결정
                // - 오늘 촬영 이력 없음 → 새 StudyID (yyyyMMdd0001)
                // - 오늘 촬영 이력 있음 → "이어서 촬영?" 팝업 → 네/아니오 선택
                string resolvedStudyId = await ResolveStudyId(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString()
                );

                // 결정된 StudyID 의 폴더가 이미 존재하는지 확인
                // → 존재함 = 이어서 촬영 선택 (기존 StudyID)
                // → 없음   = 새로 촬영 (새 StudyID 또는 오늘 첫 촬영)
                bool isExistingStudy = IsExistingStudy(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString(),
                    resolvedStudyId
                );

                if (isExistingStudy)
                {
                    // ── 이어서 촬영 ──
                    // 사용자가 "이어서 촬영하시겠습니까?" 에 "네" 를 선택한 경우
                    // 기존 StudyID 유지 + 마지막 이미지/영상 인덱스 복원
                    // 예) 오전에 이미지 3장, 영상 2개 찍어뒀다면
                    //     _currentInstanceIndex = 3, _currentVideoIndex = 2 로 복원
                    //     → 다음 촬영 시 이미지 4번, 영상 3번부터 이어서 저장
                    ResumeStudy(resolvedStudyId);
                    _currentVideoIndex = GetLastVideoIndex(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        resolvedStudyId
                    );

                    // 기존 파일이 있으므로 썸네일 즉시 로드
                    _ = RefreshThumbnailsAsync();
                }
                else
                {
                    // ── 새로 촬영 ──
                    // 아래 두 가지 경우 모두 해당
                    // 1) 오늘 이 환자의 촬영 이력이 아예 없는 경우
                    // 2) "이어서 촬영하시겠습니까?" 에 "아니오" 를 선택한 경우
                    //    → 마지막 StudyID + 1 의 새 StudyID 로 시작
                    //    예) 기존 마지막이 202503160001 이면 → 202503160002 로 시작
                    StartNewStudy(resolvedStudyId);
                    _currentVideoIndex = 0;
                    // 새 StudyID 라 파일 없음 → 썸네일 로드 불필요
                }
            }
            // ═══════════════════════════════════════════
            //  StudyID 가 이미 있는 경우
            //  → ImageComment / VideoComment 에서 복귀한 상황
            //  → StudyID 는 유지, 인덱스만 0 이면 복원
            //  예) ImageComment 에서 back 버튼 → new Scan(patient, studyId)
            //      이 때 _currentInstanceIndex, _currentVideoIndex 는 0 으로 초기화됨
            //      → 폴더 스캔해서 마지막 인덱스 복원
            // ═══════════════════════════════════════════
            else
            {
                // 이미지 인덱스 복원
                // _currentInstanceIndex 가 0 이면 Comment 복귀 후 초기화된 상태
                // → DICOM/Image/ 폴더 스캔해서 마지막 번호 복원
                if (_currentInstanceIndex == 0)
                {
                    _currentInstanceIndex = GetLastInstanceIndex(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        _currentStudyId
                    );
                }

                // 영상 인덱스 복원
                // _currentVideoIndex 가 0 이면 Comment 복귀 후 초기화된 상태
                // → VIDEO/ 폴더 스캔해서 마지막 번호 복원 (Del_ 파일 포함)
                if (_currentVideoIndex == 0)
                {
                    _currentVideoIndex = GetLastVideoIndex(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        _currentStudyId
                    );
                }
            }
        }

        /// <summary>오늘의 StudyID 목록 수집</summary>
        private List<string> GetTodayStudyIds(string patientName, string patientCode)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string today = DateTime.Now.ToString("yyyyMMdd");
            var result = new HashSet<string>();

            // ── DICOM 폴더 탐색 ──
            string dicomTodayDir = Path.Combine(
                Common.executablePath, "DICOM", patientFolderName, today);

            if (Directory.Exists(dicomTodayDir))
            {
                foreach (string dir in Directory.GetDirectories(dicomTodayDir))
                {
                    string folderName = Path.GetFileName(dir);
                    if (folderName.StartsWith(today) && folderName.Length == 12)
                    {
                        //   추가 - 유효한 파일 있을 때만 목록에 추가
                        if (HasValidFiles(folderName, patientFolderName))
                            result.Add(folderName);
                    }
                }
            }

            // ── VIDEO 폴더도 탐색 ──
            string videoTodayDir = Path.Combine(
                Common.executablePath, "VIDEO", patientFolderName, today);

            if (Directory.Exists(videoTodayDir))
            {
                foreach (string dir in Directory.GetDirectories(videoTodayDir))
                {
                    string folderName = Path.GetFileName(dir);
                    if (folderName.StartsWith(today) && folderName.Length == 12)
                    {
                        //   추가 - 유효한 파일 있을 때만 목록에 추가
                        if (HasValidFiles(folderName, patientFolderName))
                            result.Add(folderName);
                    }
                }
            }

            var list = result.ToList();
            list.Sort();
            return list;
        }

        // ═══════════════════════════════════════════
        //  StudyID 폴더 안에 유효한 파일이 하나라도 있는지 확인
        //
        //  호출 시점:
        //  GetTodayStudyIds() 에서 StudyID 목록 만들 때
        //  폴더가 있어도 파일이 전부 Del_ 이면 없는 것으로 판단
        //
        //  체크하는 폴더 3곳:
        //  ① DICOM/환자폴더/날짜/StudyID/Image  → 이미지 촬영 파일 (.dcm)
        //  ② DICOM/환자폴더/날짜/StudyID/Video  → DICOM 영상 파일 (.dcm)
        //  ③ VIDEO/환자폴더/날짜/StudyID        → AVI Only 파일 (.avi)
        //
        //  셋 중 하나라도 Del_ 아닌 파일이 있으면 → true (유효한 StudyID)
        //  셋 다 없거나 전부 Del_ 이면 → false (새 StudyID 로 시작)
        // ═══════════════════════════════════════════
        private bool HasValidFiles(string studyId, string patientFolderName)
        {
            string today = studyId.Substring(0, 8);

            // ① 이미지 촬영 파일 체크
            // 이미지 캡처를 했다면 여기에 .dcm 파일이 있음
            string imageDir = Path.Combine(Common.executablePath, "DICOM",
                patientFolderName, today, studyId, "Image");

            if (Directory.Exists(imageDir) &&
                Directory.GetFiles(imageDir, "*.dcm")
                         .Any(f => !Path.GetFileName(f).StartsWith("Del_")))
                return true;  // Del_ 아닌 dcm 파일 있음 → 유효

            // ② DICOM 영상 파일 체크
            // Dicom Record 녹화를 했다면 여기에 .dcm 파일이 있음
            string dicomVideoDir = Path.Combine(Common.executablePath, "DICOM",
                patientFolderName, today, studyId, "Video");

            if (Directory.Exists(dicomVideoDir) &&
                Directory.GetFiles(dicomVideoDir, "*.dcm")
                         .Any(f => !Path.GetFileName(f).StartsWith("Del_")))
                return true;  // Del_ 아닌 dcm 파일 있음 → 유효

            // ③ AVI Only 파일 체크
            // AVI Only 녹화를 했다면 여기에 .avi 파일이 있음
            string aviDir = Path.Combine(Common.executablePath, "VIDEO",
                patientFolderName, today, studyId);

            if (Directory.Exists(aviDir) &&
                Directory.GetFiles(aviDir, "*.avi")
                         .Any(f => !Path.GetFileName(f).StartsWith("Del_")))
                return true;  // Del_ 아닌 avi 파일 있음 → 유효

            // ① ② ③ 전부 없거나 전부 Del_ → 유효한 파일 없음
            return false;
        }

        /// <summary>다음 StudyID 계산 - 마지막 StudyID seq + 1</summary>
        private string GetNextStudyId(List<string> todayStudyIds)
        {
            string today = DateTime.Now.ToString("yyyyMMdd");

            if (todayStudyIds == null || todayStudyIds.Count == 0)
                return today + "0001";

            string lastStudyId = todayStudyIds[todayStudyIds.Count - 1];
            string seqText = lastStudyId.Substring(8, 4);

            if (!int.TryParse(seqText, out int seq)) seq = 0;
            seq++;

            return today + seq.ToString("D4");
        }

        /// <summary>마지막 StudyID 반환</summary>
        private string GetLastStudyId(List<string> todayStudyIds)
        {
            if (todayStudyIds == null || todayStudyIds.Count == 0) return null;
            todayStudyIds.Sort();
            return todayStudyIds[todayStudyIds.Count - 1];
        }

        /// <summary>
        /// 최종 StudyID 결정
        /// 오늘 촬영 이력 있으면 이어서/새로 촬영 선택 팝업
        /// </summary>
        private async Task<string> ResolveStudyId(string patientName, string patientCode)
        {
            var todayStudyIds = GetTodayStudyIds(patientName, patientCode);

            if (todayStudyIds.Count == 0)
                return DateTime.Now.ToString("yyyyMMdd") + "0001";

            string lastStudyId = GetLastStudyId(todayStudyIds);
            string nextStudyId = GetNextStudyId(todayStudyIds);

            var result = await CustomMessageWindow.ShowAsync(
                $"오늘 촬영된 이미지가 존재합니다.\n\n기존 촬영을 이어서 촬영하시겠습니까?",
                CustomMessageWindow.MessageBoxType.YesNo,
                0,
                CustomMessageWindow.MessageIconType.Info);

            return result == CustomMessageWindow.MessageBoxResult.Yes
                ? lastStudyId
                : nextStudyId;
        }

        /// <summary>새 StudyID 시작 - 인덱스 초기화</summary>
        private void StartNewStudy(string studyId)
        {
            _currentStudyId = studyId;
            _currentInstanceIndex = 0;
        }

        /// <summary>기존 StudyID 이어서 - 마지막 이미지 인덱스 복원</summary>
        private void ResumeStudy(string studyId)
        {
            _currentStudyId = studyId;
            _currentInstanceIndex = GetLastInstanceIndex(
                SelectedPatient.PatientName,
                SelectedPatient.PatientCode.ToString(),
                studyId
            );
        }

        /// <summary>StudyID 존재 여부 확인 (폴더 존재 여부로 판단)</summary>
        private bool IsExistingStudy(string patientName, string patientCode, string studyId)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string studyDateFolder = studyId.Substring(0, 8);

            string dicomDir = Path.Combine(Common.executablePath, "DICOM",
                patientFolderName, studyDateFolder, studyId);
            string videoDir = Path.Combine(Common.executablePath, "VIDEO",
                patientFolderName, studyDateFolder, studyId);

            return Directory.Exists(dicomDir) || Directory.Exists(videoDir);
        }

        /// <summary>Image 폴더에서 마지막 이미지 번호 읽기</summary>
        private int GetLastInstanceIndex(string patientName, string patientCode, string studyId)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string studyDateFolder = studyId.Substring(0, 8);
            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string imageDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyId, "Image");

            if (!Directory.Exists(imageDir)) return 0;

            int maxIndex = 0;
            foreach (string file in Directory.GetFiles(imageDir, "*.dcm"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string[] parts = fileName.Split('_');
                if (parts.Length == 0) continue;
                string lastPart = parts[parts.Length - 1];
                if (int.TryParse(lastPart, out int idx) && idx > maxIndex)
                    maxIndex = idx;
            }
            return maxIndex;
        }

        /// <summary>
        /// VIDEO 폴더에서 마지막 동영상 번호 읽기
        /// Dicom Record(_Dicom.avi) + AVI Only(_Avi.avi) 전체 스캔
        /// Del_ 파일도 번호에 포함 → 복구 시 인덱스 충돌 방지
        /// </summary>
        private int GetLastVideoIndex(string patientName, string patientCode, string studyId)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string studyDateFolder = studyId.Substring(0, 8);
            string videoDir = Path.Combine(
                Common.executablePath, "VIDEO",
                patientFolderName, studyDateFolder, studyId);

            if (!Directory.Exists(videoDir)) return 0;

            int maxIndex = 0;
            foreach (string file in Directory.GetFiles(videoDir, "*.avi"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                // Del_ 접두사 제거 후 파싱
                // Del_박한용_2634_202503130001_3_Avi → 박한용_2634_202503130001_3_Avi
                if (fileName.StartsWith("Del_"))
                    fileName = fileName.Substring(4);

                // 파일명: 박한용_2634_202503130001_3_Avi
                //                                  ↑ 뒤에서 두번째가 인덱스
                string[] parts = fileName.Split('_');
                if (parts.Length < 2) continue;
                if (int.TryParse(parts[parts.Length - 2], out int idx) && idx > maxIndex)
                    maxIndex = idx;
            }
            return maxIndex;
        }

        private async Task UpdatePatientAfterScan()
        {
            try
            {
                var repo = new DB_Manager();

                DateTime now = DateTime.Now;

                // 핵심: 촬영 직후 마지막 촬영일시 갱신
                SelectedPatient.LastShootDate = now;

                // 실제 촬영 날짜 수 기준으로 ShotNum 재계산
                SelectedPatient.ShotNum = await GetShotDateCount(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString()
                );

                // 1. LOCAL 환자 -> 기존 row만 갱신
                if (SelectedPatient.Source == PatientSource.Local)
                {
                    SelectedPatient.SourceType = (int)PatientSourceType.Local;
                    SelectedPatient.Source = PatientSource.Local;
                    SelectedPatient.IsEmrPatient = false;

                    repo.UpdateLocalPatientAfterScan(SelectedPatient);
                    return;
                }

                // 2. EMR 환자 -> 촬영 후 E-SYNC로 저장
                if (SelectedPatient.Source == PatientSource.Emr ||
                    SelectedPatient.Source == PatientSource.ESync)
                {
                    var esyncPatient = repo.GetPatientByCodeAndSource(
                        SelectedPatient.PatientCode,
                        (int)PatientSourceType.ESync
                    );

                    if (esyncPatient == null)
                    {
                        SelectedPatient.SourceType = (int)PatientSourceType.ESync;
                        SelectedPatient.Source = PatientSource.ESync;
                        SelectedPatient.IsEmrPatient = true;

                        // SelectedPatient에 LastShootDate/ShotNum이 이미 세팅됨
                        repo.UpsertEmrPatient(SelectedPatient);
                    }
                    else
                    {
                        esyncPatient.LastShootDate = now;
                        esyncPatient.ShotNum = await GetShotDateCount(
                            SelectedPatient.PatientName,
                            SelectedPatient.PatientCode.ToString()
                        );

                        repo.UpsertEmrPatient(esyncPatient);
                    }

                    return;
                }

                Common.WriteSessionLog(
                    $"UpdatePatientAfterScan: 알 수 없는 환자 Source={SelectedPatient.Source}, SourceType={SelectedPatient.SourceType}");
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region 페이지 이동

        private async Task OpenImageComment()
        {
            try
            {
                if (_isVideoRecording || _isDicomRecording)
                {
                    await CustomMessageWindow.ShowAsync(
                        "영상 녹화중에는 코멘트 창으로 이동이 불가능합니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_currentStudyId))
                {
                    await CustomMessageWindow.ShowAsync(
                        "불러올 촬영 이미지가 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                string patientFolderName = $"{SelectedPatient.PatientName}_{SelectedPatient.PatientCode}";
                string studyDateFolder = _currentStudyId.Substring(0, 8);
                string imageDir = Path.Combine(
                    Common.executablePath, "DICOM",
                    patientFolderName, studyDateFolder,
                    _currentStudyId, "Image"
                );

                if (!Directory.Exists(imageDir))
                {
                    await CustomMessageWindow.ShowAsync("촬영된 이미지가 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                bool hasDicom = Directory.EnumerateFiles(imageDir, "*.dcm").Any(f => !Path.GetFileName(f).StartsWith("Del_"));
                if (!hasDicom)
                {
                    await CustomMessageWindow.ShowAsync("촬영된 이미지가 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                MainPage.Instance.NavigateTo(
                    new ImageComment_Page.ImageComment(SelectedPatient, _currentStudyId));
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync("이미지 코멘트 화면으로 이동할 수 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
            }
        }

        private async Task OpenVideoComment()
        {
            try
            {
                if (_isVideoRecording || _isDicomRecording)
                {
                    await CustomMessageWindow.ShowAsync(
                        "영상 녹화중에는 코멘트 창으로\n이동이 불가능합니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // StudyID 없으면 영상 촬영 자체가 없는 상태
                if (string.IsNullOrWhiteSpace(_currentStudyId))
                {
                    await CustomMessageWindow.ShowAsync(
                        "불러올 촬영 영상이 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                string patientFolderName = $"{SelectedPatient.PatientName}_{SelectedPatient.PatientCode}";
                string studyDateFolder = _currentStudyId.Substring(0, 8);

                // IMAGE → VIDEO 폴더로 변경
                string videoDir = Path.Combine(
                    Common.executablePath, "VIDEO",
                    patientFolderName, studyDateFolder,
                    _currentStudyId
                );

                if (!Directory.Exists(videoDir))
                {
                    await CustomMessageWindow.ShowAsync("촬영된 영상이 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // Del_ 파일 제외하고 AVI 존재 여부 확인
                bool hasVideo = Directory.EnumerateFiles(videoDir, "*.avi")
                    .Any(f => !Path.GetFileName(f).StartsWith("Del_"));

                if (!hasVideo)
                {
                    await CustomMessageWindow.ShowAsync("촬영된 영상이 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                MainPage.Instance.NavigateTo(new VideoComment_Page.VideoComment(SelectedPatient, _currentStudyId));
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync("영상 코멘트 화면으로 이동할 수 없습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
            }
        }

        private void NavigateToPatient() =>
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());

        private void NavigateToImageReview() =>
            MainPage.Instance.NavigateTo(new ImageReview(_selectedPatient));

        private void NavigateToVideoReview() =>
            MainPage.Instance.NavigateTo(new VideoReview(_selectedPatient));

        #endregion

        #region 메뉴 액션

        private void ToggleMenu()
        {
            if (!IsMenuOpen && (DateTime.UtcNow - _menuLastClosed).TotalMilliseconds < 200)
                return;
            IsMenuOpen = !IsMenuOpen;
        }

        private async Task ExecuteLock()
        {
            IsMenuOpen = false;

            var result = await CustomMessageWindow.ShowAsync(
                "프로그램을 잠금하시겠습니까?",
                CustomMessageWindow.MessageBoxType.YesNo,
                0,
                CustomMessageWindow.MessageIconType.Info);

            if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

            // 잠금 중 세션 타이머 정지 (lock ↔ unlock은 하나의 세션으로 묶음)
            App.ActivityMonitor.Stop();

            // 현재 창을 숨기고 잠금 화면(SessionLogin) 표시
            SessionStateManager.SuspendSession();
            var sessionLoginWindow = new SessionLogin();
            sessionLoginWindow.Show();
            Application.Current.MainWindow = sessionLoginWindow;
        }

        private async Task ExecuteLogout()
        {
            IsMenuOpen = false;
            await Common.ExecuteLogout();
        }

        private async Task ExecuteExit()
        {
            IsMenuOpen = false;
            await Common.ExcuteExit();
        }

        #endregion

        #region 썸네일 추출
        // ═══════════════════════════════════════════
        //  썸네일 갱신
        //  StudyID 기준으로 마지막 이미지/영상 파일 첫 프레임 추출
        //  호출 시점:
        //    1. EnsureStudyReady() 완료 후
        //    2. ResumeStudy() 완료 후
        //    3. 이미지/영상 촬영 완료 후
        // ═══════════════════════════════════════════
        public async Task RefreshThumbnailsAsync()
        {
            try
            {
                if (SelectedPatient == null || string.IsNullOrEmpty(_currentStudyId)) return;

                // 병렬로 동시에 추출 (속도 향상)
                var imgTask = Task.Run(() => LoadImageThumbnail());
                var vidTask = Task.Run(() => LoadVideoThumbnail());

                await Task.WhenAll(imgTask, vidTask);

                // UI 스레드에서 프로퍼티 업데이트
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    ImageThumbnail = imgTask.Result;
                    VideoThumbnail = vidTask.Result;
                });
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ── 마지막 DCM 파일 첫 프레임 추출 ──
        private async Task<ImageSource> LoadImageThumbnail()
        {
            try
            {
                string patientFolderName = $"{SelectedPatient.PatientName}_{SelectedPatient.PatientCode}";
                string imageDir = Path.Combine(
                    Common.executablePath, "DICOM",
                    patientFolderName,
                    _currentStudyId.Substring(0, 8),
                    _currentStudyId, "Image");

                if (!Directory.Exists(imageDir)) return null;

                string lastFile = Directory.GetFiles(imageDir, "*.dcm")
                    .Where(f => !Path.GetFileName(f).StartsWith("Del_"))
                    .OrderBy(f => f)
                    .LastOrDefault();

                if (lastFile == null) return null;

                // ── ImageComment 와 완전히 동일한 방식 ──
                // DicomImage.RenderImage() → fo-dicom 이 색상 처리
                using (var dicomFile = DicomFile.Open(lastFile))
                {
                    var dicomImage = new DicomImage(dicomFile.Dataset);
                    var rendered = dicomImage.RenderImage();
                    var pixels = rendered.As<byte[]>();

                    var bitmap = new WriteableBitmap(
                        rendered.Width, rendered.Height, 96, 96,
                        System.Windows.Media.PixelFormats.Bgra32, null);

                    bitmap.WritePixels(
                        new System.Windows.Int32Rect(0, 0, rendered.Width, rendered.Height),
                        pixels, rendered.Width * 4, 0);

                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); return null; }
        }

        // ── 마지막 AVI 파일 첫 프레임 추출 ──
        private async Task<ImageSource> LoadVideoThumbnail()
        {
            try
            {
                string patientFolderName = $"{SelectedPatient.PatientName}_{SelectedPatient.PatientCode}";
                string videoDir = Path.Combine(
                    Common.executablePath, "VIDEO",
                    patientFolderName,
                    _currentStudyId.Substring(0, 8),
                    _currentStudyId);

                if (!Directory.Exists(videoDir)) return null;

                // Del_ 제외 + 마지막 파일
                string lastFile = Directory.GetFiles(videoDir, "*.avi")
                    .Where(f => !Path.GetFileName(f).StartsWith("Del_"))
                    .OrderBy(f => f)
                    .LastOrDefault();

                if (lastFile == null) return null;

                // AVI 첫 프레임 추출
                using (var cap = new VideoCapture(lastFile))
                {
                    if (!cap.IsOpened()) return null;

                    using (var frame = new Mat())
                    {
                        cap.Read(frame);
                        if (frame.Empty()) return null;

                        return await ConvertMatToBitmapSource(frame);
                    }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); return null; }
        }

        // ── Mat → BitmapSource 변환 (UI 스레드 안전) ──
        private async Task<BitmapSource> ConvertMatToBitmapSource(Mat mat)
        {
            try
            {
                // Mat → byte[] → BitmapSource 직접 변환
                int width = mat.Width;
                int height = mat.Height;
                int channels = mat.Channels();
                int stride = width * channels;

                byte[] buffer = new byte[height * stride];
                System.Runtime.InteropServices.Marshal.Copy(
                    mat.Data, buffer, 0, buffer.Length);

                var pixelFormat = channels == 1
                    ? System.Windows.Media.PixelFormats.Gray8
                    : System.Windows.Media.PixelFormats.Bgr24;

                var bitmap = BitmapSource.Create(
                    width, height,
                    96, 96,
                    pixelFormat,
                    null,
                    buffer,
                    stride);

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return null; }
        }


        #endregion

        #region 카메라 제어

        private async Task ConnectCamera()
        {
            await Task.Run(async () =>
            {
                try
                {
                    bool success = _cameraService.Connect();
                    if (!success)
                    {
                        Console.WriteLine("> 카메라 없음 → 테스트 영상 모드");
                        _cameraService.StartTestVideo(TEST_VIDEO_PATH);
                        return;
                    }

                    DB_Manager db = new DB_Manager();
                    DefaultModel data = db.GetDefaultSet();
                    if (data != null)
                    {
                        await _cameraService.InitializeCameraSettings(data);
                        Application.Current?.Dispatcher.Invoke(() => GainText = $"{data.Gain:F1} dB");
                        ExposureText = $"{data.ExposureTime / 1000000:F1}s";
                        GainText = $"{data.Gain:F1} dB";
                        GammaValue = data.Gamma;
                        IrisValue = data.Iris;
                        FilterValue = data.Filter;
                    }
                    else Console.WriteLine("> DB 기본값 없음 → 카메라 기본값 사용");

                    await _cameraService.StartLiveView();
                    GainText = $"{await _cameraService.GainCurrentRead():F1} dB";
                }
                catch (Exception ex) { OnCameraError($"카메라 스레드 오류: {ex.Message}"); }
            });
        }

        // ── 프레임 도착 → 이미지 스캔 가능 ──
        private void OnFrameArrived(WriteableBitmap bitmap)
        {
            if (_disposed) return;

            _isFrameReady = true;
            CanImageScan = true;
            CameraStatus = "Camera Ready";

            Application.Current?.Dispatcher.Invoke(
                () => PreviewSource = bitmap, DispatcherPriority.Render);
        }

        // ── 카메라 끊김 → 이미지 스캔 불가능 + 테스트 영상 모드 ──
        private void OnCameraDisconnected()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _isFrameReady = false;
                CanImageScan = false;
                CameraStatus = "Camera Disconnected";
                _cameraService.StartTestVideo(TEST_VIDEO_PATH);
            });
        }

        private void OnCameraReconnected() =>
            Application.Current?.Dispatcher.Invoke(() => Console.WriteLine("> 카메라 재연결 완료"));

        private async void OnCameraError(string message)
        {
            await Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                Console.WriteLine($"오류 : {message}");
                Common.WriteSessionLog(message);
                await CustomMessageWindow.ShowAsync(message,
                    CustomMessageWindow.MessageBoxType.Ok, 2,
                    CustomMessageWindow.MessageIconType.Warning);
            });
        }

        #endregion

        #region 카메라 설정 제어

        private void ToggleColorMap()
        {
            string prev = _cameraService.ColorMap;
            if (_cameraService.ColorMap == "Origin") _cameraService.ColorMap = "Rainbow";
            else if (_cameraService.ColorMap == "Rainbow") _cameraService.ColorMap = "Invert";
            else _cameraService.ColorMap = "Origin";
            Console.WriteLine($"> 컬러맵 변경: {prev} → {_cameraService.ColorMap}");
        }

        private async Task ResetValue()
        {
            var confirm = await CustomMessageWindow.ShowAsync(
                "기본 셋팅값으로 초기화하시겠습니까?",
                CustomMessageWindow.MessageBoxType.YesNo, 0,
                CustomMessageWindow.MessageIconType.Info);

            if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

            DB_Manager db = new DB_Manager();
            DefaultModel data = db.GetDefaultSet();
            if (data == null) return;

            await _cameraService.InitializeCameraSettings(data);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                ExposureText = $"{data.ExposureTime / 1000000:F1}s";
                GainText = $"{data.Gain:F1} dB";
                GammaValue = data.Gamma;
                IrisValue = data.Iris;
            });

            await CustomMessageWindow.ShowAsync("초기화 되었습니다.",
                CustomMessageWindow.MessageBoxType.Ok, 1,
                CustomMessageWindow.MessageIconType.Info);
        }

        public SolidColorBrush FilterOnBackground =>
            FilterValue == 1
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

        public SolidColorBrush FilterOffBackground =>
            FilterValue == 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

        private async Task OnFilterOn()
        {
            await LensCtrl.Instance.OptFilterMove(1);
            FilterValue = 1;
        }

        private async Task OnFilterOff()
        {
            await LensCtrl.Instance.OptFilterMove(0);
            FilterValue = 0;
        }

        private async Task OnExposureInc()
        {
            await _cameraService.ExposureInc();
            ExposureText = $"{await _cameraService.ExposureCurrentRead() / 1000000:F1}s";
        }

        private async Task OnExposureDec()
        {
            await _cameraService.ExposureDec();
            ExposureText = $"{await _cameraService.ExposureCurrentRead() / 1000000:F1}s";
        }

        private async Task OnGainInc()
        {
            await _cameraService.GainInc();
            GainText = $"{await _cameraService.GainCurrentRead():F1} dB";
        }

        private async Task OnGainDec()
        {
            await _cameraService.GainDec();
            GainText = $"{await _cameraService.GainCurrentRead():F1} dB";
        }

        private async Task OnGammaInc()
        {
            await _cameraService.GammaInc();
            GammaValue = await _cameraService.GammaCurrentRead();
        }

        private async Task OnGammaDec()
        {
            await _cameraService.GammaDec();
            GammaValue = await _cameraService.GammaCurrentRead();
        }

        private async Task OnIrisInc()
        {
            await _cameraService.IrisInc();
            IrisValue = await _cameraService.IrisCurrentRead();
        }

        private async Task OnIrisDec()
        {
            await _cameraService.IrisDec();
            IrisValue = await _cameraService.IrisCurrentRead();
        }

        private async Task OnZoomInc()
        {
            try { await _cameraService.ZoomIn(); ZoomText = $"{LensCtrl.Instance.zoomCurrentAddr}"; }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }
        private async Task OnZoomDec()
        {
            try { await _cameraService.ZoomOut(); ZoomText = $"{LensCtrl.Instance.zoomCurrentAddr}"; }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }
        private async Task OnFocusInc()
        {
            try { await  _cameraService.FocusIn(); FocusText = $"{LensCtrl.Instance.focusCurrentAddr}"; }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }
        private async Task OnFocusDec()
        {
            try { await  _cameraService.FocusOut(); FocusText = $"{LensCtrl.Instance.focusCurrentAddr}"; }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region 헬퍼

        /// <summary>생년월일로 나이 계산</summary>
        private int CalculateAge(DateTime birthDate)
        {
            int age = DateTime.Today.Year - birthDate.Year;
            if (birthDate.Date > DateTime.Today.AddYears(-age)) age--;
            return age;
        }

        /// <summary>
        /// 환자의 실제 촬영 날짜 수를 계산
        /// 기준:
        /// - DICOM/환자폴더/날짜/StudyID/Image 또는 Video 안에 유효 파일이 있으면 그 날짜를 1일로 계산
        /// - VIDEO/환자폴더/날짜/StudyID 안에 유효 avi 파일이 있어도 그 날짜를 1일로 계산
        /// - 같은 날짜에 여러 StudyID가 있어도 날짜 1개로만 계산
        /// </summary>
        private async Task<int> GetShotDateCount(string patientName, string patientCode)
        {
            try
            {
                string patientFolderName = $"{patientName}_{patientCode}";
                var shotDates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string dicomPatientDir = Path.Combine(Common.executablePath, "DICOM", patientFolderName);
                string videoPatientDir = Path.Combine(Common.executablePath, "VIDEO", patientFolderName);

                // 1. DICOM 폴더 기준 날짜 수집
                if (Directory.Exists(dicomPatientDir))
                {
                    foreach (string dateDir in Directory.GetDirectories(dicomPatientDir))
                    {
                        string dateFolderName = Path.GetFileName(dateDir);

                        if (!Regex.IsMatch(dateFolderName, @"^\d{8}$"))
                            continue;

                        bool hasValidDicom = Directory.GetFiles(dateDir, "*.*", SearchOption.AllDirectories)
                            .Any(f =>
                            {
                                string ext = Path.GetExtension(f).ToLowerInvariant();
                                string fileName = Path.GetFileName(f);

                                return (ext == ".dcm") && !fileName.StartsWith("Del_");
                            });

                        if (hasValidDicom)
                            shotDates.Add(dateFolderName);
                    }
                }

                // 2. VIDEO 폴더 기준 날짜 수집
                if (Directory.Exists(videoPatientDir))
                {
                    foreach (string dateDir in Directory.GetDirectories(videoPatientDir))
                    {
                        string dateFolderName = Path.GetFileName(dateDir);

                        if (!Regex.IsMatch(dateFolderName, @"^\d{8}$"))
                            continue;

                        bool hasValidVideo = Directory.GetFiles(dateDir, "*.*", SearchOption.AllDirectories)
                            .Any(f =>
                            {
                                string ext = Path.GetExtension(f).ToLowerInvariant();
                                string fileName = Path.GetFileName(f);

                                return (ext == ".avi") && !fileName.StartsWith("Del_");
                            });

                        if (hasValidVideo)
                            shotDates.Add(dateFolderName);
                    }
                }

                return shotDates.Count;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return 0;
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Dicom Record 녹화 중이면 중지
            _recordCts?.Cancel();
            _recordLoopTask?.Wait(1000);
            _videoWriter?.Release();
            _videoWriter?.Dispose();

            // AVI Only 녹화 중이면 중지
            _aviOnlyCts?.Cancel();
            _aviOnlyLoopTask?.Wait(1000);
            _aviOnlyWriter?.Release();
            _aviOnlyWriter?.Dispose();

            _cameraService.ErrorOccurred -= OnCameraError;
            _cameraService.FrameArrived -= OnFrameArrived;
            _cameraService.CameraDisconnected -= OnCameraDisconnected;
            _cameraService.CameraReconnected -= OnCameraReconnected;

            try { _cameraService.StopLiveView(); } catch { }
            _cameraService.Dispose();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}