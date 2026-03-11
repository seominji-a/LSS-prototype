using FellowOakDicom;
using LSS_prototype.Common_Module;
using LSS_prototype.DB_CRUD;
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

        private string _currentStudyId;
        private string _currentSeriesNumber;
        private int _currentInstanceIndex = 0;

        private bool _isFrameReady = false; //프레임 준비 여부 플래그 추가-프레임이 준비되기 전에는 촬영 버튼 방지

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

        // ─────────────────────────────────────────────
        // 생성자
        // ─────────────────────────────────────────────

        public ScanViewModel(PatientModel selectedPatient)
        {
            SelectedPatient = selectedPatient;
            NavigatePatientCommand = new RelayCommand(NavigateToPatient);
            LogoutCommand = new RelayCommand(Common.ExecuteLogout);
            ExitCommand = new RelayCommand(Common.ExcuteExit);
            ColorMapCommand = new RelayCommand(ToggleColorMap);

            _cameraService.FrameArrived += OnFrameArrived;
            _cameraService.ErrorOccurred += OnCameraError;
            _cameraService.SharpnessUpdated += (val) => Sharpness = $"{val:F2}";
            _cameraService.CameraDisconnected += OnCameraDisconnected;
            _cameraService.CameraReconnected += OnCameraReconnected;
            _currentSeriesNumber = GenerateSeriesNumber(SelectedPatient.PatientId.ToString());
            _currentInstanceIndex = 0;

            ConnectCamera();

            ZoomIncCommand = new RelayCommand(OnZoomInc);
            ZoomDecCommand = new RelayCommand(OnZoomDec);
            FocusIncCommand = new RelayCommand(OnFocusInc);
            FocusDecCommand = new RelayCommand(OnFocusDec);
            AutoFocusCommand = new RelayCommand(OnAutoFocus);
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
            ImageScanCommand = new RelayCommand(async _ => await CaptureAndSaveDicomAsync());
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
                string seriesNumber = _currentSeriesNumber;

                // 촬영할 때마다 1 증가
                _currentInstanceIndex++;
                int instanceIndex = _currentInstanceIndex;

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

                    StartNewStudy(studyId);
                }

                String studyID = _currentStudyId;

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
                    seriesNumber,
                    "",
                    date,
                    time
                );

                dm.SetContent(seriesNumber, date, time, instanceIndex.ToString());
                dm.SetPrivateDataElement(exposure, gain, gamma);

                string path = GenerateSavePath(
                        SelectedPatient.PatientName,
                        SelectedPatient.PatientCode.ToString(),
                        studyID,
                        seriesNumber,
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
        private string GenerateSeriesNumber(string patientId)
        {
            string numericId = new string(patientId.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(numericId))
                numericId = "1";

            if (numericId.Length >= 4)
                numericId = numericId.Substring(numericId.Length - 4);

            numericId = numericId.TrimStart('0');
            if (string.IsNullOrEmpty(numericId))
                numericId = "1";

            return numericId + DateTime.Now.ToString("HHmmss");
        }

        /// <summary>
        /// DICOM 파일 저장 경로 생성
        /// 예: (exe 위치)\DICOM\홍길동_1234_12340001_0.dcm
        /// </summary>
        private string GenerateSavePath(string name, string code, string studyID, string seriesNumber, int instanceIndex)
        {
            string patientFolderName = $"{name}_{code}";

            string rootDir = Path.Combine(Common.executablePath, "DICOM");
            string patientDir = Path.Combine(rootDir, patientFolderName);
            string studyDir = Path.Combine(patientDir, studyID);
            string seriesDir = Path.Combine(studyDir, seriesNumber);

            Directory.CreateDirectory(seriesDir);

            string fileName = $"{patientFolderName}_{seriesNumber}_{instanceIndex}.dcm";
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

            foreach (string dir in Directory.GetDirectories(patientDir))
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
        }
        // ─────────────────────────────────────────────
        // 기존 메서드들
        // ─────────────────────────────────────────────

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

        private void OnCameraDisconnected() =>
            Application.Current?.Dispatcher.Invoke(() => _cameraService.StartTestVideo(TEST_VIDEO_PATH));

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

        private void OnFrameArrived(WriteableBitmap bitmap)
        {
            if (_disposed) return;

            _isFrameReady = true;

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