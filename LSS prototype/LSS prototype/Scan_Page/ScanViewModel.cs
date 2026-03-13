using FellowOakDicom;
using LSS_prototype.Common_Module;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Dicom_Module;
using LSS_prototype.Lens_Module;
using LSS_prototype.Patient_Page;
using LSS_prototype.User_Page;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

        // ── DICOM 녹화 관련 ──
        private VideoWriter _videoWriter;            // 영상기록 객체
        private string _aviSavePath;                 // AVI 최종 저장 경로
        private bool _isDicomRecording = false;      // 현재 녹화 중인지 여부
        private CancellationTokenSource _recordCts;  // 녹화 중지 신호
        private DateTime _recordStartTime;           // 녹화 시작 시간
        private int _currentVideoIndex = 0;          // 동영상 인스턴스 번호
        private Task _recordLoopTask;                // RecordLoop Task (중지 시 완료 대기용)
        private int _lastRecordingSecond = -1;       // UI 경과시간 업데이트 최적화용

        // ── 촬영 관련 ──
        private string _currentStudyId;
        private int _currentInstanceIndex = 0;
        private bool _isFrameReady = false;          // 프레임 준비 전 촬영 방지

        private static readonly string TEST_VIDEO_PATH = Path.Combine(Common.executablePath, "sample.avi");
        private readonly ScanModel _img = new ScanModel();

        #endregion

        #region 바인딩 프로퍼티

        // ── 선택된 환자 ──
        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set { _selectedPatient = value; OnPropertyChanged(); }
        }

        // ── 녹화 상태 ──
        private bool _isDicomRecordingProp;
        public bool IsDicomRecording
        {
            get => _isDicomRecordingProp;
            set { _isDicomRecordingProp = value; OnPropertyChanged(); }
        }

        // ── 녹화 경과시간 표시 ──
        private string _recordingTime = "00:00";
        public string RecordingTime
        {
            get => _recordingTime;
            set { _recordingTime = value; OnPropertyChanged(); }
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

        #endregion

        #region 커맨드

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
        public ICommand ImageScanCommand { get; }
        public ICommand ImageCommentCommand { get; }
        public ICommand DicomRecordCommand { get; }

        #endregion

        #region 생성자

        public ScanViewModel(PatientModel selectedPatient, string studyId = null)
        {
            SelectedPatient = selectedPatient;
            _currentStudyId = studyId;

            NavigatePatientCommand = new RelayCommand(NavigateToPatient);
            LogoutCommand = new RelayCommand(Common.ExecuteLogout);
            ExitCommand = new RelayCommand(Common.ExcuteExit);
            ColorMapCommand = new RelayCommand(ToggleColorMap);

            _cameraService.FrameArrived += OnFrameArrived;
            _cameraService.ErrorOccurred += OnCameraError;
            _cameraService.SharpnessUpdated += (val) => Sharpness = $"{val:F2}";
            _cameraService.CameraDisconnected += OnCameraDisconnected;
            _cameraService.CameraReconnected += OnCameraReconnected;

            // 초반에는 프레임 도착 전이므로 이미지 스캔 가능 여부를 false로 고정
            CanImageScan = false;

            // 프레임 도착 전, 카메라 준비 상태 사용자에게 확인 목적
            CameraStatus = "Camera Initializing...";

            ConnectCamera();

            ZoomIncCommand = new RelayCommand(OnZoomInc);
            ZoomDecCommand = new RelayCommand(OnZoomDec);
            FocusIncCommand = new RelayCommand(OnFocusInc);
            FocusDecCommand = new RelayCommand(OnFocusDec);
            AutoFocusCommand = new RelayCommand(OnAutoFocus);
            GainIncCommand = new RelayCommand(OnGainInc);
            GainDecCommand = new RelayCommand(OnGainDec);
            ExposureIncCommand = new RelayCommand(OnExposureInc);
            ExposureDecCommand = new RelayCommand(OnExposureDec);
            GammaIncCommand = new RelayCommand(OnGammaInc);
            GammaDecCommand = new RelayCommand(OnGammaDec);
            IrisIncCommand = new RelayCommand(OnIrisInc);
            IrisDecCommand = new RelayCommand(OnIrisDec);
            ResetSettingCommand = new RelayCommand(ResetValue);
            FilterOnCommand = new RelayCommand(OnFilterOn);
            FilterOffCommand = new RelayCommand(OnFilterOff);
            ImageScanCommand = new RelayCommand(async _ => await CaptureAndSaveDicomAsync());
            ImageCommentCommand = new RelayCommand(OpenImageComment);
            DicomRecordCommand = new RelayCommand(async _ => await ToggleDicomRecord());
        }

        #endregion

        #region 이미지 캡처

        // ═══════════════════════════════════════════
        //  촬영 전 유효성 검사
        //  환자 선택 여부, 카메라 상태, 프레임 준비 여부 확인
        //  문제 있으면 false 반환 + 팝업 표시
        // ═══════════════════════════════════════════
        private bool CaptureValidation()
        {
            if (SelectedPatient == null)
            {
                CustomMessageWindow.Show("환자를 먼저 선택해주세요.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return false;
            }

            if (CameraStatus == "Camera Disconnected")
            {
                CustomMessageWindow.Show("카메라가 연결되어 있지 않습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return false;
            }

            if (!_isFrameReady)
            {
                CustomMessageWindow.Show("카메라 영상이 \n 아직 준비되지 않았습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return false;
            }

            return true;
        }

        // ═══════════════════════════════════════════
        //  이미지 캡처 및 DICOM 저장
        //  1) 유효성 검사 (CaptureValidation)
        //  2) StudyID / instanceIndex 확정 (EnsureStudyReady)
        //  3) 프레임 캡처 → DICOM 변환 → 저장
        // ═══════════════════════════════════════════
        private async Task CaptureAndSaveDicomAsync()
        {
            System.Drawing.Bitmap bitmap = null;
            Mat frame = null;

            try
            {
                // ① 유효성 검사
                if (!CaptureValidation()) return;

                // ② 프레임 캡처
                frame = _cameraService.GetCurrentFrame();
                if (frame == null || frame.Empty())
                {
                    CustomMessageWindow.Show("촬영할 이미지가 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                bitmap = BitmapConverter.ToBitmap(frame);

                string date = DateTime.Now.ToString("yyyyMMdd");
                string time = DateTime.Now.ToString("HHmmss");

                string accessionNumber = (SelectedPatient.Dataset != null)
                    ? SelectedPatient.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "")
                    : "";

                double exposure = _cameraService.ExposureCurrentRead();
                double gain = _cameraService.GainCurrentRead();
                double gamma = _cameraService.GammaCurrentRead();

                var db = new DB_Manager();
                var setting = db.GetPacsSet();
                string hospName = setting?.HospitalName ?? "";

                // ③ StudyID / instanceIndex 확정
                EnsureStudyReady();

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
                string path = GenerateSavePath(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString(),
                    _currentStudyId,
                    instanceIndex
                );

                await dm.SaveImageFile(path, bitmap);

                CustomMessageWindow.Show("촬영 및 저장이 완료되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                CustomMessageWindow.Show($"촬영/저장 중 오류가 발생했습니다.\n{ex.Message}",
                    CustomMessageWindow.MessageBoxType.AutoClose, 3,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            finally
            {
                // 사용한 리소스 반드시 해제
                frame?.Dispose();
                bitmap?.Dispose();
            }
        }

        // ── 이미지 DCM 저장 경로 생성 ──
        // DICOM/
        //     └── 박한용_2634/
        //         └── 20250313/
        //             └── 202503130001/
        //                 ├── Image/
        //                 │   ├── 박한용_2634_202503130001_1.dcm
        //                 │   └── 박한용_2634_202503130001_2.dcm
        //                 └── Video/
        //                     └── 박한용_2634_202503130001_1.dcm
        private string GenerateSavePath(string name, string code, string studyID, int instanceIndex)
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

        #region 동영상 녹화

        // ═══════════════════════════════════════════
        //  녹화 토글
        //  녹화 중이면 → 중지
        //  녹화 중 아니면 → 유효성 검사 후 시작
        // ═══════════════════════════════════════════
        private async Task ToggleDicomRecord()
        {
            try
            {
                if (_isDicomRecording)
                    await StopDicomRecord();
                else
                {
                    if (!CaptureValidation()) return;
                    StartDicomRecord();
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  녹화 시작
        //  ① 현재 프레임 확인
        //  ② StudyID 확정
        //  ③ VideoWriter 초기화 성공 확인 후 VideoIndex 증가
        //  ④ 녹화 상태 ON
        //  ⑤ RecordLoop Task 시작
        //  ⑥ 1분 자동 중지 타이머
        // ═══════════════════════════════════════════
        private void StartDicomRecord()
        {
            try
            {
                // ① 현재 프레임 확인
                var frame = _cameraService.GetCurrentFrame();
                if (frame == null || frame.Empty())
                {
                    CustomMessageWindow.Show("카메라 영상이 준비되지 않았습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // ② StudyID 확정
                EnsureStudyReady();

                // ③ VideoWriter 초기화 먼저 확인
                // VideoWriter 실패 시 VideoIndex 증가하면 안 되므로
                // 성공 확인 후 VideoIndex 증가
                double fps = _cameraService.GetCurrentFps();
                string tempAviPath = GenerateVideoSavePath(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString(),
                    _currentStudyId,
                    _currentVideoIndex + 1  // 증가될 값 미리 계산
                );

                _videoWriter = new VideoWriter(
                    tempAviPath,
                    FourCC.MJPG,  // 압축 방식 (용량 작고 빠름)
                    fps,
                    new OpenCvSharp.Size(frame.Width, frame.Height)
                );

                if (!_videoWriter.IsOpened())
                {
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                    frame.Dispose();
                    CustomMessageWindow.Show("녹화를 시작할 수 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
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

                // ⑤ RecordLoop Task 변수로 저장 (중지 시 완료 대기에 사용)
                _recordLoopTask = Task.Run(() => RecordLoop(_recordCts.Token, fps));

                // ⑥ 1분 자동 중지 타이머
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), _recordCts.Token);
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            CustomMessageWindow.Show("1분 녹화 완료. 자동 저장합니다.",
                                CustomMessageWindow.MessageBoxType.AutoClose, 2,
                                CustomMessageWindow.MessageIconType.Info);
                            await StopDicomRecord();
                        });
                    }
                    catch (OperationCanceledException) { } // 수동 중지 시 정상 취소
                });

                frame.Dispose();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  프레임 캡처 루프
        //  Cancel 신호 올 때까지 계속 실행
        //
        //  핵심 아이디어:
        //  프레임 처리 시간이 매번 다르기 때문에
        //  무조건 33ms Sleep 하면 실제 fps가 낮아짐
        //  해결: 목표간격 - 처리시간 = 남은 시간만 Sleep
        //       → 항상 정확한 fps 유지
        // ═══════════════════════════════════════════
        private void RecordLoop(CancellationToken ct, double fps)
        {
            // 목표 간격 계산 (예: 5fps → 200ms, 30fps → 33ms)
            var targetInterval = TimeSpan.FromMilliseconds(1000.0 / fps);

            while (!ct.IsCancellationRequested)
            {
                var frameStart = DateTime.Now;

                try
                {
                    // 현재 프레임 가져와서 AVI 에 기록
                    var frame = _cameraService.GetCurrentFrame();
                    if (frame != null && !frame.Empty())
                    {
                        _videoWriter?.Write(frame);
                        frame.Dispose(); // Mat 은 쓰고나서 바로 해제 (안하면 메모리 누수)
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
                catch (Exception ex) { Common.WriteLog(ex); break; }

                // 프레임 처리 시간 빼고 남은 시간만 Sleep
                // 예) 처리 10ms → 200 - 10 = 190ms Sleep → 실제 간격 200ms 유지
                var frameElapsed = DateTime.Now - frameStart;
                var remaining = targetInterval - frameElapsed;
                if (remaining > TimeSpan.Zero)
                    Thread.Sleep(remaining);
            }
        }

        // ═══════════════════════════════════════════
        //  녹화 중지 + AVI 저장 + DICOM 변환
        //  ① 녹화 중지 신호
        //  ② RecordLoop 완전 종료 대기
        //     → 종료 확인 후 VideoWriter Release 해야 AVI 파일 안전
        //  ③ VideoWriter 닫기
        //  ④ AVI → DICOM 변환 저장
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
                if (string.IsNullOrEmpty(_aviSavePath) || !File.Exists(_aviSavePath))
                {
                    _currentVideoIndex--;
                    CustomMessageWindow.Show("저장된 영상 파일이 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // ⑤ DICOM 변환 시작 (로딩창 표시)
                LoadingWindow.Begin("DICOM 변환 중...");

                await Task.Run(() =>
                {
                    string date = DateTime.Now.ToString("yyyyMMdd");
                    string time = DateTime.Now.ToString("HHmmss");

                    var db = new DB_Manager();
                    var setting = db.GetPacsSet();
                    string hospName = setting?.HospitalName ?? "";

                    string accessionNumber = (SelectedPatient.Dataset != null)
                        ? SelectedPatient.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "")
                        : "";

                    // ⑥ DicomManager 생성 (이미지 캡처랑 동일한 패턴)
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

                    // ⑦ DCM 저장 경로 생성
                    // DICOM/박한용_2634/20250313/202503130001/Video/박한용_2634_202503130001_1.dcm
                    string dcmPath = GenerateDcmVideoSavePath(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        _currentStudyId,
                        _currentVideoIndex
                    );

                    // ⑧ AVI → DICOM 변환 저장
                    dm.SaveVideoFile(dcmPath, _aviSavePath);
                });

                LoadingWindow.End();

                CustomMessageWindow.Show("동영상 저장 완료.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                LoadingWindow.End(); // 에러 나도 반드시 로딩창 닫기
                Common.WriteLog(ex);
                CustomMessageWindow.Show($"동영상 저장 실패: {ex.Message}",
                    CustomMessageWindow.MessageBoxType.AutoClose, 3,
                    CustomMessageWindow.MessageIconType.Warning);
            }
        }

        // ── AVI 저장 경로 생성 ──
        // VIDEO/박한용_2634/20250313/202503130001/박한용_2634_202503130001_1.avi
        private string GenerateVideoSavePath(string name, string code, string studyID, int videoIndex)
        {
            string patientFolderName = $"{name}_{code}";
            string studyDateFolder = studyID.Substring(0, 8);
            string rootDir = Path.Combine(Common.executablePath, "VIDEO");
            string videoDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyID);
            Directory.CreateDirectory(videoDir);
            string fileName = $"{patientFolderName}_{studyID}_{videoIndex}.avi";
            return Path.Combine(videoDir, fileName);
        }

        // ── 동영상 DCM 저장 경로 생성 ──
        // DICOM/박한용_2634/20250313/202503130001/Video/박한용_2634_202503130001_1.dcm
        private string GenerateDcmVideoSavePath(string name, string code, string studyID, int videoIndex)
        {
            string patientFolderName = $"{name}_{code}";
            string studyDateFolder = studyID.Substring(0, 8);
            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string videoDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyID, "Video");
            Directory.CreateDirectory(videoDir);
            string fileName = $"{patientFolderName}_{studyID}_{videoIndex}.dcm";
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
        private void EnsureStudyReady()
        {
            if (string.IsNullOrEmpty(_currentStudyId))
            {
                // 처음 촬영 → StudyID 결정
                string studyId = ResolveStudyId(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString()
                );

                bool exists = IsExistingStudy(
                    SelectedPatient.PatientName,
                    SelectedPatient.PatientCode.ToString(),
                    studyId
                );

                if (exists)
                {
                    // 기존 StudyID → 이미지/동영상 마지막 인덱스 복원
                    ResumeStudy(studyId);
                    _currentVideoIndex = GetLastVideoIndex(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        studyId
                    );
                }
                else
                {
                    // 새 StudyID → 인덱스 0 으로 시작
                    StartNewStudy(studyId);
                    _currentVideoIndex = 0;
                }
            }
            else
            {
                // StudyID 있음 → Comment/Review 에서 돌아온 경우
                // instanceIndex 가 0 이면 마지막 인덱스 복원
                if (_currentInstanceIndex == 0)
                {
                    _currentInstanceIndex = GetLastInstanceIndex(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        _currentStudyId
                    );
                }

                // 동영상 인덱스도 동일하게 복원
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
            string patientDir = Path.Combine(Common.executablePath, "DICOM", patientFolderName);
            var result = new List<string>();

            if (!Directory.Exists(patientDir)) return result;

            string today = DateTime.Now.ToString("yyyyMMdd");
            string todayDir = Path.Combine(patientDir, today);

            if (!Directory.Exists(todayDir)) return result;

            foreach (string dir in Directory.GetDirectories(todayDir))
            {
                string folderName = Path.GetFileName(dir);
                // 형식: yyyyMMdd0001 (12자리)
                if (folderName.StartsWith(today) && folderName.Length == 12)
                    result.Add(folderName);
            }

            result.Sort();
            return result;
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
        private string ResolveStudyId(string patientName, string patientCode)
        {
            var todayStudyIds = GetTodayStudyIds(patientName, patientCode);

            // 오늘 촬영 이력 없으면 무조건 0001
            if (todayStudyIds.Count == 0)
                return DateTime.Now.ToString("yyyyMMdd") + "0001";

            string lastStudyId = GetLastStudyId(todayStudyIds);
            string nextStudyId = GetNextStudyId(todayStudyIds);

            var result = CustomMessageWindow.Show(
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
            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string studyDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyId);
            return Directory.Exists(studyDir);
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

        /// <summary>Video 폴더에서 마지막 동영상 번호 읽기</summary>
        private int GetLastVideoIndex(string patientName, string patientCode, string studyId)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string studyDateFolder = studyId.Substring(0, 8);
            string videoDir = Path.Combine(
                Common.executablePath, "DICOM",
                patientFolderName, studyDateFolder, studyId, "Video");

            if (!Directory.Exists(videoDir)) return 0;

            int maxIndex = 0;
            foreach (string file in Directory.GetFiles(videoDir, "*.dcm"))
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

        #endregion

        #region 페이지 이동

        // ── 이미지 코멘트 페이지 이동 ──
        private void OpenImageComment()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_currentStudyId))
                {
                    CustomMessageWindow.Show(
                        "불러올 촬영 이미지가 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
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
                    CustomMessageWindow.Show(
                        "촬영된 이미지가 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                bool hasDicom = Directory.EnumerateFiles(imageDir, "*.dcm").Any();
                if (!hasDicom)
                {
                    CustomMessageWindow.Show(
                        "촬영된 이미지가 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                MainPage.Instance.NavigateTo(
                    new ImageComment_Page.ImageComment(SelectedPatient, _currentStudyId));
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                CustomMessageWindow.Show(
                    "이미지 코멘트 화면으로 이동할 수 없습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
            }
        }

        private void NavigateToPatient() =>
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());

        #endregion

        #region 카메라 제어

        // ── 카메라 연결 및 초기 설정 ──
        private void ConnectCamera()
        {
            Task.Run(() =>
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
                        _cameraService.InitializeCameraSettings(data);
                        Application.Current?.Dispatcher.Invoke(() => GainText = $"{data.Gain:F1} dB");
                        ExposureText = $"{data.ExposureTime / 1000000:F1}s";
                        GainText = $"{data.Gain:F1} dB";
                        GammaValue = data.Gamma;
                        IrisValue = data.Iris;
                        FilterValue = data.Filter;
                    }
                    else Console.WriteLine("> DB 기본값 없음 → 카메라 기본값 사용");

                    _cameraService.StartLiveView();
                    GainText = $"{_cameraService.GainCurrentRead():F1} dB";
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

        private void OnCameraError(string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"오류 : {message}");
                Common.WriteSessionLog(message);
                CustomMessageWindow.Show(message,
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
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

        private void ResetValue()
        {
            var confirm = CustomMessageWindow.Show(
                "기본 셋팅값으로 초기화하시겠습니까?",
                CustomMessageWindow.MessageBoxType.YesNo, 0,
                CustomMessageWindow.MessageIconType.Info);

            if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

            DB_Manager db = new DB_Manager();
            DefaultModel data = db.GetDefaultSet();
            if (data == null) return;

            _cameraService.InitializeCameraSettings(data);

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

        public SolidColorBrush FilterOnBackground =>
            FilterValue == 1
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

        public SolidColorBrush FilterOffBackground =>
            FilterValue == 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3F55"));

        private void OnFilterOn() { LensCtrl.Instance.OptFilterMove(1); FilterValue = 1; }
        private void OnFilterOff() { LensCtrl.Instance.OptFilterMove(0); FilterValue = 0; }
        private void OnExposureInc() { _cameraService.ExposureInc(); ExposureText = $"{_cameraService.ExposureCurrentRead() / 1000000:F1}s"; }
        private void OnExposureDec() { _cameraService.ExposureDec(); ExposureText = $"{_cameraService.ExposureCurrentRead() / 1000000:F1}s"; }
        private void OnGainInc() { _cameraService.GainInc(); GainText = $"{_cameraService.GainCurrentRead():F1} dB"; }
        private void OnGainDec() { _cameraService.GainDec(); GainText = $"{_cameraService.GainCurrentRead():F1} dB"; }
        private void OnGammaInc() { _cameraService.GammaInc(); GammaValue = _cameraService.GammaCurrentRead(); }
        private void OnGammaDec() { _cameraService.GammaDec(); GammaValue = _cameraService.GammaCurrentRead(); }
        private void OnIrisInc() { _cameraService.IrisInc(); IrisValue = _cameraService.IrisCurrentRead(); }
        private void OnIrisDec() { _cameraService.IrisDec(); IrisValue = _cameraService.IrisCurrentRead(); }
        private async void OnAutoFocus() => await _cameraService.AutoFocus();
        private void OnZoomInc()
        {
            try { _cameraService.ZoomIn(); ZoomText = $"{LensCtrl.Instance.zoomCurrentAddr}"; }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
        private void OnZoomDec()
        {
            try { _cameraService.ZoomOut(); ZoomText = $"{LensCtrl.Instance.zoomCurrentAddr}"; }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
        private void OnFocusInc()
        {
            try { _cameraService.FocusIn(); FocusText = $"{LensCtrl.Instance.focusCurrentAddr}"; }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
        private void OnFocusDec()
        {
            try { _cameraService.FocusOut(); FocusText = $"{LensCtrl.Instance.focusCurrentAddr}"; }
            catch (Exception ex) { Common.WriteLog(ex); }
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

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 녹화 중이면 중지 신호 + RecordLoop 완료 대기
            _recordCts?.Cancel();
            _recordLoopTask?.Wait(1000);
            _videoWriter?.Release();
            _videoWriter?.Dispose();

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
