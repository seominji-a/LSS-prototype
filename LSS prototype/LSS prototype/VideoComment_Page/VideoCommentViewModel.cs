using FellowOakDicom;
using LSS_prototype.Auth;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Login_Page;
using LSS_prototype.Patient_Page;
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

namespace LSS_prototype.VideoComment_Page
{
    public class VideoCommentViewModel : INotifyPropertyChanged
    {
        // ═══════════════════════════════════════════
        //  경로
        //  VIDEO/박한용_2634/20250313/202503130001/*.avi
        // ═══════════════════════════════════════════
        private string VideoDir => Path.Combine(
            Common.executablePath, "VIDEO",
            $"{_patient.PatientName}_{_patient.PatientCode}",
            _studyId.Substring(0, 8),
            _studyId);

        private readonly PatientModel _patient;
        private readonly string _studyId;
        private readonly string _emrcheck;

        // ── AVI 파일 목록 + 현재 인덱스 ──
        private List<string> _videoFiles = new List<string>();
        private int _currentIndex = 0;

        // ── 배속 단계 ──
        private readonly double[] _slowSteps = { 1.0, 0.5, 0.25, 0.166 };
        private readonly string[] _slowLabels = { "1x", "x0.5", "x0.25", "x0.16" };
        private readonly double[] _fastSteps = { 1.0, 2.0, 4.0, 6.0 };
        private readonly string[] _fastLabels = { "1x", "x2", "x4", "x6" };

        private static readonly SolidColorBrush SpeedNormalBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        private static readonly SolidColorBrush SpeedSlowBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
        private static readonly SolidColorBrush SpeedFastBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

        // ── MediaElement 조작 Action (xaml.cs 에서 주입) ──
        private Action _seekBackAction;
        private Action _seekForwardAction;
        private Action _playPauseAction;

        // ── 이벤트 ──
        public event Action RequestNavigateToScan;

        // ═══════════════════════════════════════════
        //  변경 감지 플래그
        //  IsCommentDirty → CommentText setter에서 자동 세팅
        //  페이지 이동/나가기 시 저장 팝업 여부 판단
        // ═══════════════════════════════════════════
        public bool IsCommentDirty { get; private set; } = false;

        #region 바인딩 프로퍼티

        private string _currentVideoPath;
        public string CurrentVideoPath
        {
            get => _currentVideoPath;
            private set { _currentVideoPath = value; OnPropertyChanged(); }
        }

        private string _currentFileName;
        public string CurrentFileName
        {
            get => _currentFileName;
            private set
            {
                _currentFileName = value;
                OnPropertyChanged();
                VideoType = value != null && value.Contains("Dicom")
                    ? "DICOM VIDEO"
                    : "AVI VIDEO";
            }
        }

        private string _pageIndicator;
        public string PageIndicator
        {
            get => _pageIndicator;
            private set { _pageIndicator = value; OnPropertyChanged(); }
        }

        public string PatientName => $"{_patient.DisplayName} ({_patient.PatientCode})";
        public string PatientInfo => $"{_patient.BirthDate:yyyy-MM-dd} / {_patient.Sex}";

        private string _selectedPosition;
        public string SelectedPosition
        {
            get => _selectedPosition;
            set { _selectedPosition = value; OnPropertyChanged(); }
        }

        private string _selectedAnatomical;
        public string SelectedAnatomical
        {
            get => _selectedAnatomical;
            set { _selectedAnatomical = value; OnPropertyChanged(); }
        }

        // CommentText: setter에서 IsCommentDirty 자동 세팅
        //   UpdateCurrentFile / Reset 에서는 필드 직접 할당 후 OnPropertyChanged
        //   → setter 통하면 IsCommentDirty=true 되므로 반드시 이 방식 사용
        private string _commentText;
        public string CommentText
        {
            get => _commentText;
            set
            {
                if (_commentText == value) return;
                _commentText = value;
                OnPropertyChanged();
                IsCommentDirty = true;
            }
        }

        private string _speedLabel = "1x";
        public string SpeedLabel
        {
            get => _speedLabel;
            private set { _speedLabel = value; OnPropertyChanged(); }
        }

        private SolidColorBrush _speedLabelColor = SpeedNormalBrush;
        public SolidColorBrush SpeedLabelColor
        {
            get => _speedLabelColor;
            private set { _speedLabelColor = value; OnPropertyChanged(); }
        }

        private double _currentSpeedRatio = 1.0;
        public double CurrentSpeedRatio
        {
            get => _currentSpeedRatio;
            set  // TwoWay 슬라이더 바인딩용 (0.25 단위로 스냅)
            {
                var snapped = Math.Round(value * 4.0) / 4.0;
                snapped = Math.Max(0.25, Math.Min(4.0, snapped));
                if (Math.Abs(_currentSpeedRatio - snapped) < 0.001) return;
                var label = snapped == 1.0 ? "x1" : $"x{snapped:0.##}";
                var mode  = snapped < 1.0 ? SpeedMode.Slow
                          : snapped > 1.0 ? SpeedMode.Fast : SpeedMode.Normal;
                ApplySpeed(snapped, label, mode);
            }
        }

        // ── 속도 팝업 상태 ──
        private bool _isSpeedPopupOpen;
        private DateTime _speedPopupLastClosed = DateTime.MinValue;
        public bool IsSpeedPopupOpen
        {
            get => _isSpeedPopupOpen;
            set
            {
                if (_isSpeedPopupOpen && !value) _speedPopupLastClosed = DateTime.UtcNow;
                _isSpeedPopupOpen = value;
                OnPropertyChanged();
            }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayIconVisibility));
                OnPropertyChanged(nameof(PauseIconVisibility));
            }
        }
        // 재생 아이콘: 정지 상태일 때 표시
        public Visibility PlayIconVisibility  => _isPlaying ? Visibility.Collapsed : Visibility.Visible;
        // 일시정지 아이콘: 재생 중일 때 표시
        public Visibility PauseIconVisibility => _isPlaying ? Visibility.Visible   : Visibility.Collapsed;

        private string _videoType;
        public string VideoType
        {
            get => _videoType;
            private set { _videoType = value; OnPropertyChanged(); }
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

        public PatientModel Patient { get; }
        public string StudyId { get; }

        #endregion

        #region 커맨드

        public ICommand VideoDeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand SlowerCommand { get; }
        public ICommand FasterCommand { get; }
        public ICommand SeekBackCommand { get; }
        public ICommand SeekForwardCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand NavigateBackCommand { get; }
        public ICommand ToggleMenuCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand LockCommand { get; }
        public ICommand ToggleSpeedPopupCommand { get; }
        public ICommand SpeedUpCommand { get; }
        public ICommand SpeedDownCommand { get; }
        public ICommand SetSpeedCommand { get; }

        #endregion

        #region 생성자

        public VideoCommentViewModel(PatientModel patient, string studyId, string emrcheck)
        {
            _patient = patient;
            _studyId = studyId;
            _emrcheck = emrcheck;
            Patient = patient;
            StudyId = studyId;

            VideoDeleteCommand = new RelayCommand(async _ => await ExecuteVideoDelete());
            LogoutCommand = new AsyncRelayCommand(async _ => await ExecuteLogout());
            ExitCommand = new AsyncRelayCommand(async _ => await ExecuteExit());
            LockCommand = new AsyncRelayCommand(async _ => await ExecuteLock());
            ToggleMenuCommand = new RelayCommand(_ => ToggleMenu());
            ToggleSpeedPopupCommand = new RelayCommand(_ => ToggleSpeedPopup());
            SpeedUpCommand   = new RelayCommand(_ => ExecuteSpeedUp());
            SpeedDownCommand = new RelayCommand(_ => ExecuteSpeedDown());
            SetSpeedCommand  = new RelayCommand(p  => ExecuteSetSpeed(p));
            SeekBackCommand = new RelayCommand(_ => _seekBackAction?.Invoke());
            SeekForwardCommand = new RelayCommand(_ => _seekForwardAction?.Invoke());
            PlayPauseCommand = new RelayCommand(_ => _playPauseAction?.Invoke());
            NavigateBackCommand = new RelayCommand(async _ => await ExecuteNavigateBack());
            ResetCommand = new RelayCommand(_ => Reset());

            SaveCommand = new AsyncRelayCommand(async _ =>
            {
                bool save = await ConfirmSaveAll();
                if (save) await SaveCommentAsync();
            });
        }

        #endregion

        #region MediaElement Action 주입

        public void SetMediaActions(Action seekBack, Action seekForward, Action playPause)
        {
            _seekBackAction = seekBack;
            _seekForwardAction = seekForward;
            _playPauseAction = playPause;
        }

        #endregion

        #region 저장 여부 확인 팝업 (통합)

        // ═══════════════════════════════════════════
        //  저장 여부 확인 팝업
        //  페이지 이동 / 나가기 / SAVE 버튼 공통 사용
        //  ImageComment의 ConfirmSaveAll과 동일한 패턴
        // ═══════════════════════════════════════════
        public async Task<bool> ConfirmSaveAll()
        {
            try
            {
                if (!IsCommentDirty)
                {
                    await CustomMessageWindow.ShowAsync(
                        "변경된 사항이 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    return false;
                }

                var result = await CustomMessageWindow.ShowAsync(
                    "코멘트를 저장하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Warning);
                return result == CustomMessageWindow.MessageBoxResult.Yes;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        #endregion

        #region 플래그 리셋

        public void ResetDirty()
        {
            IsCommentDirty = false;
        }

        #endregion

        #region 초기화

        public async Task<bool> Initialize()
        {
            try
            {
                if (!Directory.Exists(VideoDir))
                {
                    await CustomMessageWindow.ShowAsync(
                        "재생할 영상 파일이 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _videoFiles = Directory.GetFiles(VideoDir, "*.avi")
                    .Where(f => !Path.GetFileName(f).StartsWith("Del_"))
                    .OrderBy(f => ExtractIndex(Path.GetFileNameWithoutExtension(f)))
                    .ToList();

                if (_videoFiles.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync(
                        "재생할 영상 파일이 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _currentIndex = _videoFiles.Count - 1;
                await UpdateCurrentFile();
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        #endregion

        #region 페이지 이동

        // ═══════════════════════════════════════════
        //  이동 가능 여부 (ImageComment의 CanNavigate와 동일한 패턴)
        //  false면 코드비하인드에서 저장 팝업 없이 바로 리턴
        // ═══════════════════════════════════════════
        public bool CanNavigate(bool goNext)
        {
            int target = goNext ? _currentIndex + 1 : _currentIndex - 1;
            return target >= 0 && target < _videoFiles.Count;
        }

        public async Task<bool> MovePrev()
        {
            try
            {
                if (_currentIndex <= 0) return false;
                _currentIndex--;
                await UpdateCurrentFile();
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        public async Task<bool> MoveNext()
        {
            try
            {
                if (_currentIndex >= _videoFiles.Count - 1) return false;
                _currentIndex++;
                await UpdateCurrentFile ();
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        // ═══════════════════════════════════════════
        //  현재 파일 갱신 + 코멘트 로드
        //
        //  CommentText 로드 시 필드 직접 할당 후 OnPropertyChanged
        //  → setter 통하면 IsCommentDirty=true 되므로 반드시 이 방식 사용
        //
        //  DICOM_VIDEO → dcm 태그에서 로드
        //  NORMAL_VIDEO → COMMENT TB에서 로드
        // ═══════════════════════════════════════════
        private async Task UpdateCurrentFile()
        {
            if (_videoFiles.Count == 0) return;

            string filePath = _videoFiles[_currentIndex];
            CurrentVideoPath = filePath;
            CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
            PageIndicator = $"{_currentIndex + 1:D2}/{_videoFiles.Count:D2}";

            // 코멘트 로드 (VideoType은 CurrentFileName setter에서 이미 결정됨)
            await LoadComment(filePath);
        }

        private async Task LoadComment(string filePath)
        {
            try
            {
                string comment = string.Empty;
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                if (VideoType == "DICOM VIDEO")
                {
                    // DICOM_VIDEO → 대응하는 dcm 파일에서 태그 로드
                    string dcmPath = GetDicomPathFromAvi(filePath);
                    if (!string.IsNullOrEmpty(dcmPath) && File.Exists(dcmPath))
                    {
                        var dicomFile = DicomFile.Open(dcmPath);
                        comment = dicomFile.Dataset.GetSingleValueOrDefault(
                            DicomTag.ImageComments, string.Empty);
                    }
                }
                else
                {
                    // NORMAL_VIDEO → COMMENT TB에서 로드
                    var db = new DB_Manager();
                    comment = db.SelectComment("NORMAL_VIDEO", fileName);
                }

                //   setter 통하면 IsCommentDirty=true → 필드 직접 할당
                _commentText = comment;
                OnPropertyChanged(nameof(CommentText));
                IsCommentDirty = false;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region 코멘트 저장

        // ═══════════════════════════════════════════
        //  SaveComment (코드비하인드에서 호출)
        //
        //  DICOM_VIDEO:
        //    → dcm 태그 (ImageComments) 저장
        //    → COMMENT TB UPSERT (FILE_TYPE='DICOM_VIDEO')
        //
        //  NORMAL_VIDEO:
        //    → COMMENT TB UPSERT (FILE_TYPE='NORMAL_VIDEO')
        //
        //  저장 완료 후 IsCommentDirty 리셋
        // ═══════════════════════════════════════════
        public async Task<bool> SaveCommentAsync()
        {
            try
            {
                string filePath = _videoFiles[_currentIndex];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                var db = new DB_Manager();

                if (VideoType == "DICOM VIDEO")
                {
                    // 1. dcm 태그 저장
                    string dcmPath = GetDicomPathFromAvi(filePath);
                    if (!string.IsNullOrEmpty(dcmPath) && File.Exists(dcmPath))
                    {
                        var dicomFile = DicomFile.Open(dcmPath, FileReadOption.ReadAll);

                        if (string.IsNullOrWhiteSpace(CommentText))
                            dicomFile.Dataset.Remove(DicomTag.ImageComments);
                        else
                            dicomFile.Dataset.AddOrUpdate(DicomTag.ImageComments, CommentText);

                        string tempPath = dcmPath + ".tmp";
                        dicomFile.Save(tempPath);
                        File.Delete(dcmPath);
                        File.Move(tempPath, dcmPath);
                    }

                    // 2. COMMENT TB UPSERT
                    db.UpsertComment("DICOM_VIDEO", fileName, CommentText ?? string.Empty);
                }
                else
                {
                    // NORMAL_VIDEO → COMMENT TB만
                    db.UpsertComment("NORMAL_VIDEO", fileName, CommentText ?? string.Empty);
                }

                IsCommentDirty = false;
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        #endregion

        #region RESET

        // ═══════════════════════════════════════════
        //  RESET - 입력값 초기화
        //  CommentText는 필드 직접 할당 → IsCommentDirty=true 방지
        // ═══════════════════════════════════════════
        public void Reset()
        {
            SelectedPosition = null;
            SelectedAnatomical = null;

            _commentText = string.Empty;
            OnPropertyChanged(nameof(CommentText));
            IsCommentDirty = false;
        }

        #endregion

        #region 영상 삭제

        private async Task ExecuteVideoDelete()
        {
            try
            {
                if (_videoFiles.Count == 0) return;

                var result = await CustomMessageWindow.ShowAsync(
                    "영상을 삭제하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Warning);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                string currentFile = _videoFiles[_currentIndex];
                string dir = Path.GetDirectoryName(currentFile);
                string fileName = Path.GetFileName(currentFile);
                string deletedAviPath = Path.Combine(dir, "Del_" + fileName);

                // MediaElement 파일 잠금 해제
                CurrentVideoPath = null;

                File.Move(currentFile, deletedAviPath);

                var db = new DB_Manager();

                if (VideoType == "AVI VIDEO")
                {
                    db.InsertNormalVideoDeleteLog(
                        deletedAviPath, Patient.PatientCode, Patient.PatientName);
                    Common.WriteSessionLog(
                        $"[NORMAL VIDEO DELETE] User:{Common.CurrentUserId} " +
                        $"PatientCode:{Patient.PatientCode} " +
                        $"PatientName:{Patient.PatientName} File:{deletedAviPath}");
                }
                else
                {
                    string dcmPath = GetDicomPathFromAvi(currentFile);
                    string deletedDcmPath = null;

                    if (!string.IsNullOrEmpty(dcmPath) && File.Exists(dcmPath))
                    {
                        string dcmDir = Path.GetDirectoryName(dcmPath);
                        string dcmFileName = Path.GetFileName(dcmPath);
                        deletedDcmPath = Path.Combine(dcmDir, "Del_" + dcmFileName);
                        File.Move(dcmPath, deletedDcmPath);
                    }

                    db.InsertDicomVideoDeleteLog(
                        deletedAviPath, deletedDcmPath,
                        Patient.PatientCode, Patient.PatientName);
                    Common.WriteSessionLog(
                        $"[DICOM VIDEO DELETE] User:{Common.CurrentUserId} " +
                        $"PatientCode:{Patient.PatientCode} " +
                        $"PatientName:{Patient.PatientName} " +
                        $"AVI:{deletedAviPath} DCM:{deletedDcmPath}");
                }

                _videoFiles.RemoveAt(_currentIndex);

                if (_videoFiles.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync(
                        "영상이 삭제되었습니다.\n저장된 영상이 존재하지 않아\nScan 화면으로 이동합니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Info);
                    RequestNavigateToScan?.Invoke();
                    return;
                }

                await CustomMessageWindow.ShowAsync(
                    "비디오가 정상적으로 삭제되었습니다.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Info);

                if (_currentIndex >= _videoFiles.Count)
                    _currentIndex = _videoFiles.Count - 1;

                await UpdateCurrentFile ();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region 나가기

        private async Task ExecuteNavigateBack()
        {
            try
            {
                if (IsCommentDirty)
                {
                    bool save = await ConfirmSaveAll();
                    if (save)
                    {
                        bool success = await SaveCommentAsync();
                        if (!success) return;
                        IsCommentDirty = false;
                    }
                    else
                    {
                        IsCommentDirty = false;
                    }
                }
                MainPage.Instance.NavigateTo(new Scan_Page.Scan(Patient, _emrcheck ,StudyId));
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region 배속 제어

        private enum SpeedMode { Normal, Slow, Fast }

        private void ApplySpeed(double ratio, string label, SpeedMode mode)
        {
            _currentSpeedRatio = ratio;  // backing field 직접 설정 (setter 재귀 방지)
            OnPropertyChanged(nameof(CurrentSpeedRatio));
            SpeedLabel = label;
            SpeedLabelColor = mode == SpeedMode.Slow ? SpeedSlowBrush
                              : mode == SpeedMode.Fast ? SpeedFastBrush
                              : SpeedNormalBrush;
        }

        private void ToggleSpeedPopup()
        {
            if (!IsSpeedPopupOpen && (DateTime.UtcNow - _speedPopupLastClosed).TotalMilliseconds < 200)
                return;
            IsSpeedPopupOpen = !IsSpeedPopupOpen;
        }

        private void ExecuteSpeedUp()
        {
            var next = Math.Min(4.0, Math.Round((_currentSpeedRatio + 0.25) * 4.0) / 4.0);
            var label = next == 1.0 ? "x1" : $"x{next:0.##}";
            ApplySpeed(next, label, next < 1.0 ? SpeedMode.Slow : next > 1.0 ? SpeedMode.Fast : SpeedMode.Normal);
        }

        private void ExecuteSpeedDown()
        {
            var next = Math.Max(0.25, Math.Round((_currentSpeedRatio - 0.25) * 4.0) / 4.0);
            var label = next == 1.0 ? "x1" : $"x{next:0.##}";
            ApplySpeed(next, label, next < 1.0 ? SpeedMode.Slow : next > 1.0 ? SpeedMode.Fast : SpeedMode.Normal);
        }

        private void ExecuteSetSpeed(object parameter)
        {
            if (parameter == null) return;
            if (!double.TryParse(parameter.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double ratio)) return;
            ratio = Math.Max(0.25, Math.Min(4.0, ratio));
            var label = ratio == 1.0 ? "x1" : $"x{ratio:0.##}";
            ApplySpeed(ratio, label, ratio < 1.0 ? SpeedMode.Slow : ratio > 1.0 ? SpeedMode.Fast : SpeedMode.Normal);
        }

        public async Task ResetSpeed()
        {
            try
            {
                ApplySpeed(1.0, "1x", SpeedMode.Normal);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region 헬퍼

        // AVI 경로 → DCM 경로 변환
        // VIDEO/.../파일_Dicom.avi → DICOM/.../Video/파일_Dicom.dcm
        private string GetDicomPathFromAvi(string aviPath)
        {
            try
            {
                string videoRoot = Path.Combine(Common.executablePath, "VIDEO");
                string dicomRoot = Path.Combine(Common.executablePath, "DICOM");
                string relative = aviPath.Substring(videoRoot.Length)
                                          .TrimStart(Path.DirectorySeparatorChar);
                string dcmFileName = Path.ChangeExtension(Path.GetFileName(relative), ".dcm");
                string folderRelative = Path.GetDirectoryName(relative);
                return Path.Combine(dicomRoot, folderRelative, "Video", dcmFileName);
            }
            catch { return null; }
        }

        // 파일명에서 인덱스 번호 추출
        // 박한용_2634_202503130001_3_Avi → 3 (뒤에서 두 번째)
        private int ExtractIndex(string fileName)
        {
            string[] parts = fileName.Split('_');
            if (parts.Length < 2) return 0;
            return int.TryParse(parts[parts.Length - 2], out int idx) ? idx : 0;
        }

        #endregion

        #region  메뉴 액션
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
        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}