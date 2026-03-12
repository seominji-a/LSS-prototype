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
        // ── 카메라 서비스 ──
        private readonly CameraService _cameraService = new CameraService();
        private bool _disposed = false;

        

        private static readonly string TEST_VIDEO_PATH = Path.Combine(Common.executablePath, "sample.avi");

        // ── 촬영 이미지 리스트 ──
        private readonly ScanModel _img = new ScanModel();

        // ── 선택된 환자 (Patient 화면에서 Scan 화면으로 넘어올 때 설정) ──
        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set { _selectedPatient = value; OnPropertyChanged(); }
        }

        // ─────────────────────────────────────────────
        // UI 바인딩 프로퍼티
        // ─────────────────────────────────────────────

        private WriteableBitmap _previewSource;
        public WriteableBitmap PreviewSource
        {
            get => _previewSource;
            private set { _previewSource = value; OnPropertyChanged(); }
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

        //이미지 스캔 가능 여부 플러그
        private bool _canImageScan;
        public bool CanImageScan
        {
            get => _canImageScan;
            set
            {
                _canImageScan = value;
                OnPropertyChanged();
            }
        }

        //카메라 상태 플러그
        private string _cameraStatus = "Camera Initializing...";
        public string CameraStatus
        {
            get => _cameraStatus;
            set
            {
                _cameraStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCameraStatusVisible));
                OnPropertyChanged(nameof(IsCameraReady));
                OnPropertyChanged(nameof(IsCameraDisconnected));
                OnPropertyChanged(nameof(IsCameraInitializing));
            }
        }

        public bool IsCameraStatusVisible =>
        !string.IsNullOrWhiteSpace(CameraStatus) &&
        CameraStatus != "Camera Ready";

        public bool IsCameraReady => CameraStatus == "Camera Ready";
        public bool IsCameraDisconnected => CameraStatus == "Camera Disconnected";
        public bool IsCameraInitializing => CameraStatus == "Camera Initializing...";

        private string _currentStudyId;
        // DICOM용 숫자 시리즈 번호는 _currentSeriesIndex 사용
        private string _currentSeriesFolderName;
        private int _currentSeriesIndex = 0;
        private int _currentInstanceIndex = 0;

        private bool _isFrameReady = false; //프레임 준비 여부 플래그 추가-프레임이 준비되기 전에는 촬영 버튼 방지

        //주사 시간 상태 플러그
        private string _injectionTime;
        public string InjectionTime
        {
            get => _injectionTime;
            set
            {
                _injectionTime = value;
                OnPropertyChanged();
            }
        }

        // ─────────────────────────────────────────────
        // 커맨드
        // ─────────────────────────────────────────────

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


        public ScanViewModel(PatientModel selectedPatient, string seriesFolderName = null)
        {
            SelectedPatient = selectedPatient;

            // 코멘트/리뷰에서 다시 돌아왔을 때 기존 폴더명 유지
            _currentSeriesFolderName = seriesFolderName;
            // 시리얼 넘버가없으면 새로생성, 있으면 그대로 사용( 코멘트나 리뷰페이지에서 넘어온 경우 ) 
            // seriesNumber가 있으면 그대로 유지
            //seriesNumber가 없으면 나중에 StartNewStudy() → StartNewSeries()에서 생성

            NavigatePatientCommand = new RelayCommand(NavigateToPatient);
            LogoutCommand = new RelayCommand(Common.ExecuteLogout);
            ExitCommand = new RelayCommand(Common.ExcuteExit);
            ColorMapCommand = new RelayCommand(ToggleColorMap);

            _cameraService.FrameArrived += OnFrameArrived;
            _cameraService.ErrorOccurred += OnCameraError;
            _cameraService.SharpnessUpdated += (val) => Sharpness = $"{val:F2}";
            _cameraService.CameraDisconnected += OnCameraDisconnected;
            _cameraService.CameraReconnected += OnCameraReconnected;
            //_currentSeriesNumber = GenerateSeriesNumber(SelectedPatient.PatientId.ToString());
            //_currentInstanceIndex = 0;

            //초반에는 프레임 도착 전이므로 이미지 스캔 가능 여부를 false로 고정
            CanImageScan = false;

            //프레임 도착 전, 카메라 준비 상태 사용자에게 확인 목적-카메라 연결 시작을 의미
            CameraStatus = "Camera Initializing...";

            //Scan 진입 시 주사시간 확인
            //InitializeInjectionInfo();
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
        }

        // ─────────────────────────────────────────────
        // DICOM 저장
        // ─────────────────────────────────────────────

        private async Task CaptureAndSaveDicomAsync()
        {
            System.Drawing.Bitmap bitmap = null;
            Mat frame = null;

            try
            {
                if (SelectedPatient == null)
                {
                    CustomMessageWindow.Show("환자를 먼저 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                if (!_isFrameReady)
                {
                    CustomMessageWindow.Show("카메라 영상이 아직 준비되지 않았습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }


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

                // 촬영 세션 동안 고정
                //string seriesNumber = _currentSeriesNumber;

                // 촬영할 때마다 1 증가
                //_currentInstanceIndex++;
                //int instanceIndex = _currentInstanceIndex;

                string accessionNumber = (SelectedPatient.Dataset != null)
                    ? SelectedPatient.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "")
                    : "";

                double exposure = _cameraService.ExposureCurrentRead();
                double gain = _cameraService.GainCurrentRead();
                double gamma = _cameraService.GammaCurrentRead();

                var db = new DB_Manager();
                var setting = db.GetPacsSet();
                string hospName = setting?.HospitalName ?? "";

                string serialNumber = "00000001";

                DicomManager dm = (SelectedPatient.Dataset == null)
                    ? new DicomManager(SelectedPatient.PatientId.ToString(), serialNumber)
                    : new DicomManager(SelectedPatient.PatientId.ToString(), serialNumber, SelectedPatient.Dataset);

                dm.SetPatient(
                    SelectedPatient.PatientCode.ToString(),
                    SelectedPatient.PatientName,
                    SelectedPatient.BirthDate.ToString("yyyyMMdd"),
                    SelectedPatient.Sex,
                    CalculateAge(SelectedPatient.BirthDate).ToString()
                );

                if (string.IsNullOrEmpty(_currentStudyId))
                {
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
                        ResumeStudy(studyId);
                    else
                        StartNewStudy(studyId);
                }

                String studyID = _currentStudyId;
                // 폴더명 없으면 새 시리즈 생성
                if (string.IsNullOrEmpty(_currentSeriesFolderName))
                {
                    StartNewSeries();
                }

                string dicomSeriesNumber = _currentSeriesIndex.ToString();
                string seriesFolderName = _currentSeriesFolderName;

                _currentInstanceIndex++;
                int instanceIndex = _currentInstanceIndex;

                dm.SetStudy(
                    studyID,
                    accessionNumber,
                    date,
                    time,
                    "",
                    hospName,
                    ""
                );

                dm.SetSeries(
                    dicomSeriesNumber,
                    "",
                    date,
                    time
                );

                dm.SetContent(dicomSeriesNumber, date, time, instanceIndex.ToString());
                dm.SetPrivateDataElement(exposure, gain, gamma);

                string path = GenerateSavePath(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        studyID,
                        seriesFolderName,
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
                frame?.Dispose();
                bitmap?.Dispose();
            }
        }

        // ─────────────────────────────────────────────
        // 헬퍼 메서드
        // ─────────────────────────────────────────────

        /// <summary>
        /// 시리즈 번호 생성: 환자ID 기반
        /// </summary>
        // 폴더명 생성용
        private string GenerateSeriesFolderName()
        {
            return $"{_currentStudyId}_{_currentSeriesIndex:D2}";
        }

        /// <summary>
        /// DICOM 파일 저장 경로 생성
        /// 예: (exe 위치)\DICOM\홍길동_1234_12340001_0.dcm
        /// </summary>
        private string GenerateSavePath(string name, string code, string studyID, string seriesFolderName, int instanceIndex)
        {
            string patientFolderName = $"{name}_{code}";

            // studyID 앞 8자리 = yyyyMMdd
            string studyDateFolder = studyID.Substring(0, 8);

            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string patientDir = Path.Combine(rootDir, patientFolderName);
            string dateDir = Path.Combine(patientDir, studyDateFolder);
            string studyDir = Path.Combine(dateDir, studyID);
            string seriesDir = Path.Combine(studyDir, seriesFolderName);

            Directory.CreateDirectory(seriesDir);

            string fileName = $"{patientFolderName}_{seriesFolderName}_{instanceIndex}.dcm";
            return Path.Combine(seriesDir, fileName);
        }

        /// <summary>
        /// 생년월일로 나이 계산
        /// </summary>
        private int CalculateAge(DateTime birthDate)
        {
            int age = DateTime.Today.Year - birthDate.Year;
            if (birthDate.Date > DateTime.Today.AddYears(-age)) age--;
            return age;
        }

        // ─────────────────────────────────────────────
        // StudyID 관련 메서드들
        // ─────────────────────────────────────────────

        /// <summary>
        /// 오늘의 StudyID 목록 가져오는 함수
        /// </summary>

        private List<string> GetTodayStudyIds(string patientName, string patientCode)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string patientDir = Path.Combine(Common.executablePath, "DICOM", patientFolderName);

            var result = new List<string>();

            if (!Directory.Exists(patientDir))
                return result;

            string today = DateTime.Now.ToString("yyyyMMdd");

            // 새 구조:
            // DICOM / 환자폴더 / yyyyMMdd / studyID
            string todayDir = Path.Combine(patientDir, today);

            if (!Directory.Exists(todayDir))
                return result;

            foreach (string dir in Directory.GetDirectories(todayDir))
            {
                string folderName = Path.GetFileName(dir);

                // 형식: yyyyMMdd0001
                if (folderName.StartsWith(today) && folderName.Length == 12)
                {
                    result.Add(folderName);
                }
            }

            result.Sort();
            return result;
        }

        /// <summary>
        /// 다음 StudyID 계산 함수
        /// </summary>

        private string GetNextStudyId(List<string> todayStudyIds)
        {
            string today = DateTime.Now.ToString("yyyyMMdd");

            if (todayStudyIds == null || todayStudyIds.Count == 0)
                return today + "0001";

            string lastStudyId = todayStudyIds[todayStudyIds.Count - 1];

            string seqText = lastStudyId.Substring(8, 4);
            int seq;

            if (!int.TryParse(seqText, out seq))
                seq = 0;

            seq++;

            return today + seq.ToString("D4");
        }

        /// <summary>
        /// 마지막 StudyID 가져오는 함수
        /// </summary>
        private string GetLastStudyId(List<string> todayStudyIds)
        {
            if (todayStudyIds == null || todayStudyIds.Count == 0)
                return null;

            todayStudyIds.Sort();
            return todayStudyIds[todayStudyIds.Count - 1];
        }

        /// <summary>
        /// 최종 StudyID 결정 함수
        /// </summary>
        private string ResolveStudyId(string patientName, string patientCode)
        {
            var todayStudyIds = GetTodayStudyIds(patientName, patientCode);

            // 오늘 촬영 이력이 없으면 무조건 0001
            if (todayStudyIds.Count == 0)
                return DateTime.Now.ToString("yyyyMMdd") + "0001";

            string lastStudyId = GetLastStudyId(todayStudyIds);
            string nextStudyId = GetNextStudyId(todayStudyIds);

            var result = CustomMessageWindow.Show(
                $"오늘 촬영된 이미지가 존재합니다.\n\n" +
                $"기존 촬영을 이어서 사용하시겠습니까?\n" +
                $"- 예: {lastStudyId}\n" +
                $"- 아니오: 새 촬영 {nextStudyId}",
                CustomMessageWindow.MessageBoxType.YesNo,
                0,
                CustomMessageWindow.MessageIconType.Info);

            if (result == CustomMessageWindow.MessageBoxResult.Yes)
                return lastStudyId;

            return nextStudyId;
        }

        private void StartNewStudy(string studyId)
        {
            _currentStudyId = studyId;
            _currentSeriesIndex = 0;
            _currentSeriesFolderName = null;
            _currentInstanceIndex = 0;
        }

        /// <summary>
        /// 향후 개발 관련 사항
        /// colormap 변경, filter 변경, 촬영 모드 변경, 새 촬영 시작에 맞춰 폴더명 변경 필수!
        /// </summary>
        private void StartNewSeries()
        {
            _currentSeriesIndex++;
            _currentSeriesFolderName = GenerateSeriesFolderName();
            _currentInstanceIndex = 0;
        }

        private bool IsExistingStudy(string patientName, string patientCode, string studyId)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string studyDateFolder = studyId.Substring(0, 8);

            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string studyDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyId);

            return Directory.Exists(studyDir);
        }

        private int GetLastSeriesIndex(string patientName, string patientCode, string studyId)
        {
            string patientFolderName = $"{patientName}_{patientCode}";
            string studyDateFolder = studyId.Substring(0, 8);

            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string studyDir = Path.Combine(rootDir, patientFolderName, studyDateFolder, studyId);

            if (!Directory.Exists(studyDir))
                return 0;

            int maxIndex = 0;

            foreach (string dir in Directory.GetDirectories(studyDir))
            {
                string folderName = Path.GetFileName(dir);

                if (!folderName.StartsWith(studyId + "_"))
                    continue;

                string suffix = folderName.Substring((studyId + "_").Length);

                int idx;
                if (int.TryParse(suffix, out idx))
                {
                    if (idx > maxIndex)
                        maxIndex = idx;
                }
            }

            return maxIndex;
        }

        private void ResumeStudy(string studyId)
        {
            _currentStudyId = studyId;

            _currentSeriesIndex = GetLastSeriesIndex(
                SelectedPatient.PatientName,
                SelectedPatient.PatientCode.ToString(),
                studyId
            );

            _currentSeriesFolderName = null;
            _currentInstanceIndex = 0;
        }

        // ─────────────────────────────────────────────
        // 기존 메서드들
        // ─────────────────────────────────────────────



        private void OpenImageComment()
        {
            MainPage.Instance.NavigateTo(new ImageComment_Page.ImageComment(SelectedPatient, _currentSeriesFolderName));
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

        //프레임 도착 전에 카메라 끊김, 이미지 스캔 불가능
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

        //프레임 도착-이미지 스캔에 관한 준비를 모두 마침, 이미지 스캔 가능
        private void OnFrameArrived(WriteableBitmap bitmap)
        {
            if (_disposed) return;

            _isFrameReady = true;
            CanImageScan = true;
            CameraStatus = "Camera Ready";

            Application.Current?.Dispatcher.Invoke(
                () => PreviewSource = bitmap, DispatcherPriority.Render);
        }

        private void ToggleColorMap()
        {
            string prev = _cameraService.ColorMap;
            if (_cameraService.ColorMap == "Origin") _cameraService.ColorMap = "Rainbow";
            else if (_cameraService.ColorMap == "Rainbow") _cameraService.ColorMap = "Invert";
            else _cameraService.ColorMap = "Origin";
            Console.WriteLine($"> 컬러맵 변경: {prev} → {_cameraService.ColorMap}");
        }

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

        private void NavigateToPatient() =>
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());



        // ─────────────────────────────────────────────
        // Dispose
        // ─────────────────────────────────────────────

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