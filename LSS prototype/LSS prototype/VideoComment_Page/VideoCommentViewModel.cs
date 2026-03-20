using LSS_prototype.DB_CRUD;
using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace LSS_prototype.VideoComment_Page
{
    public class VideoCommentViewModel : INotifyPropertyChanged
    {
        // ═══════════════════════════════════════════
        //  경로
        //  VIDEO/박한용_2634/20250313/202503130001/*.avi
        //  Del_ 파일 제외, 번호순 정렬 → 촬영 순서 보장
        // ═══════════════════════════════════════════
        private string VideoDir => Path.Combine(
            Common.executablePath, "VIDEO",
            $"{_patient.PatientName}_{_patient.PatientCode}",
            _studyId.Substring(0, 8),
            _studyId);

        // ── 환자 / StudyID ──
        public PatientModel Patient { get; }
        public string StudyId { get; }

        private readonly PatientModel _patient;
        private readonly string _studyId;

        // ── AVI 파일 목록 + 현재 인덱스 ──
        private List<string> _videoFiles = new List<string>();
        private int _currentIndex = 0;

        // ── 배속 단계 ──
        private readonly double[] _slowSteps = { 1.0, 0.5, 0.25, 0.166 };
        private readonly string[] _slowLabels = { "1x", "x0.5", "x0.25", "x0.16" };
        private readonly double[] _fastSteps = { 1.0, 2.0, 4.0, 6.0 };
        private readonly string[] _fastLabels = { "1x", "x2", "x4", "x6" };

        private int _slowIndex = 0;
        private int _fastIndex = 0;

        // ── 배속 표시 색상 ──
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

        // ── 화면 이동 요청 이벤트 ──
        public event Action RequestNavigateToScan;

        #region 바인딩 프로퍼티

        // xaml.cs PropertyChanged 감지 → MediaElement.Source 변경 + 자동 재생
        private string _currentVideoPath;
        public string CurrentVideoPath
        {
            get => _currentVideoPath;
            private set { _currentVideoPath = value; OnPropertyChanged(); }
        }

        // 파일명 변경 시 VideoType 자동 결정
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

        // 현재 몇 번째 / 전체 몇 개 (Del_ 제외 목록 기준)
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

        private string _commentText;
        public string CommentText
        {
            get => _commentText;
            set { _commentText = value; OnPropertyChanged(); }
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

        // xaml.cs PropertyChanged 감지 → MediaElement.SpeedRatio 적용
        private double _currentSpeedRatio = 1.0;
        public double CurrentSpeedRatio
        {
            get => _currentSpeedRatio;
            private set { _currentSpeedRatio = value; OnPropertyChanged(); }
        }

        private string _playPauseIcon = "▶";
        public string PlayPauseIcon
        {
            get => _playPauseIcon;
            set { _playPauseIcon = value; OnPropertyChanged(); }
        }

        private string _videoType;
        public string VideoType
        {
            get => _videoType;
            private set { _videoType = value; OnPropertyChanged(); }
        }

        #endregion

        #region 커맨드

        public ICommand VideoDeleteCommand { get; }
        public ICommand CommentSaveCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand SlowerCommand { get; }
        public ICommand FasterCommand { get; }
        public ICommand SeekBackCommand { get; }
        public ICommand SeekForwardCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand NavigateBackCommand { get; }

        #endregion

        #region 생성자

        public VideoCommentViewModel(PatientModel patient, string studyId)
        {
            _patient = patient;
            _studyId = studyId;
            Patient = patient;
            StudyId = studyId;

            VideoDeleteCommand = new RelayCommand(async _ => await ExecuteVideoDelete());
            CommentSaveCommand = new RelayCommand(async _ => await ExecuteCommentSave());
            ExitCommand = new RelayCommand(async _ => await Common.ExcuteExit());
            SlowerCommand = new RelayCommand(async _ => await ExecuteSlower());
            FasterCommand = new RelayCommand(async _ => await ExecuteFaster());
            SeekBackCommand = new RelayCommand(_ => _seekBackAction?.Invoke());
            SeekForwardCommand = new RelayCommand(_ => _seekForwardAction?.Invoke());
            PlayPauseCommand = new RelayCommand(_ => _playPauseAction?.Invoke());
            NavigateBackCommand = new RelayCommand(async _ => await ExecuteNavigateBack());
        }

        #endregion

        #region MediaElement Action 주입

        // ═══════════════════════════════════════════
        //  MediaElement 조작 Action 주입
        //  ViewModel 이 MediaElement 를 직접 참조하지 않기 위해 Action 으로 분리
        //  xaml.cs OnLoaded 에서 호출
        // ═══════════════════════════════════════════
        public void SetMediaActions(Action seekBack, Action seekForward, Action playPause)
        {
            _seekBackAction = seekBack;
            _seekForwardAction = seekForward;
            _playPauseAction = playPause;
        }

        #endregion

        #region 초기화

        // ═══════════════════════════════════════════
        //  초기화 - VIDEO/ 폴더에서 AVI 파일 목록 수집
        //  Del_ 파일 제외 + 번호순 정렬
        //  xaml.cs Loaded 에서 호출
        // ═══════════════════════════════════════════
        public async Task<bool> Initialize()
        {
            try
            {
                if (!Directory.Exists(VideoDir))
                {
                    await CustomMessageWindow.ShowAsync("재생할 영상 파일이 없습니다.", CustomMessageWindow.MessageBoxType.AutoClose, 2, CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _videoFiles = Directory.GetFiles(VideoDir, "*.avi")
                    .Where(f => !Path.GetFileName(f).StartsWith("Del_"))
                    .OrderBy(f => ExtractIndex(Path.GetFileNameWithoutExtension(f)))
                    .ToList();

                if (_videoFiles.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync("재생할 영상 파일이 없습니다.", CustomMessageWindow.MessageBoxType.AutoClose, 2, CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _currentIndex = _videoFiles.Count - 1;
                UpdateCurrentFile();
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        #endregion

        #region 파일 이동

        // ═══════════════════════════════════════════
        //  이전 파일 이동
        // ═══════════════════════════════════════════
        public async Task<bool> MovePrev()
        {
            try
            {
                if (_currentIndex <= 0)
                {
                    await CustomMessageWindow.ShowAsync("첫 번째 영상입니다.", CustomMessageWindow.MessageBoxType.AutoClose, 1, CustomMessageWindow.MessageIconType.Info);
                    return false;
                }

                _currentIndex--;
                UpdateCurrentFile();
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        // ═══════════════════════════════════════════
        //  다음 파일 이동
        // ═══════════════════════════════════════════
        public async Task<bool> MoveNext()
        {
            try
            {
                if (_currentIndex >= _videoFiles.Count - 1)
                {
                    await CustomMessageWindow.ShowAsync("마지막 영상입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    return false;
                }

                _currentIndex++;
                UpdateCurrentFile();
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        // ═══════════════════════════════════════════
        //  CurrentVideoPath / FileName 갱신
        //  CurrentVideoPath 변경 → xaml.cs 트리거 → MediaElement 자동 재생
        // ═══════════════════════════════════════════
        private void UpdateCurrentFile()
        {
            if (_videoFiles.Count == 0) return;

            CurrentVideoPath = _videoFiles[_currentIndex];
            CurrentFileName = Path.GetFileNameWithoutExtension(_videoFiles[_currentIndex]);
            PageIndicator = $"{_currentIndex + 1:D2}/{_videoFiles.Count:D2}";
        }

        #endregion

        #region 영상 삭제

        // ═══════════════════════════════════════════
        //  영상 삭제
        //
        //  AVI VIDEO (NORMAL_VIDEO):
        //      AVI 파일만 Del_ 처리
        //      DB: InsertNormalVideoDeleteLog
        //
        //  DICOM VIDEO:
        //      AVI + DCM 둘 다 Del_ 처리
        //      AVI 경로 → GetDicomPathFromAvi() 로 DCM 경로 계산
        //      DB: InsertDicomVideoDeleteLog
        //
        //  삭제 후 파일 0개 → RequestNavigateToScan 발생 → Scan 화면 이동
        // ═══════════════════════════════════════════
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

                // MediaElement 가 파일을 열고 있으므로 파일 잠금 해제
                CurrentVideoPath = null;

                //  Del_ 처리
                File.Move(currentFile, deletedAviPath);

                var db = new DB_Manager();

                if (VideoType == "AVI VIDEO")
                {
                    // NORMAL_VIDEO: AVI 만 Del_
                    db.InsertNormalVideoDeleteLog(
                        deletedAviPath,
                        Patient.PatientCode,
                        Patient.PatientName);
                    Common.WriteSessionLog($"[NORMAL VIDEO DELETE] User:{Common.CurrentUserId} PatientCode:{Patient.PatientCode} " +
                        $"PatientName:{Patient.PatientName}  File:{deletedAviPath}");
                }
                else
                {
                    // DICOM_VIDEO: AVI + DCM 둘 다 Del_
                    string dcmPath = await GetDicomPathFromAvi(currentFile);
                    string deletedDcmPath = null;

                    if (!string.IsNullOrEmpty(dcmPath) && File.Exists(dcmPath))
                    {
                        string dcmDir = Path.GetDirectoryName(dcmPath);
                        string dcmFileName = Path.GetFileName(dcmPath);
                        deletedDcmPath = Path.Combine(dcmDir, "Del_" + dcmFileName);
                        File.Move(dcmPath, deletedDcmPath); // 추가적으로 한쌍인 .dcm 파일명에도 Del_을 붙여준다. 
                    }

                    db.InsertDicomVideoDeleteLog(deletedAviPath, deletedDcmPath, Patient.PatientCode, Patient.PatientName);
                    Common.WriteSessionLog($"[DICOM VIDEO DELETE] User:{Common.CurrentUserId} " +
                                           $"PatientCode:{Patient.PatientCode} PatientName:{Patient.PatientName} " +
                                           $"AVI:{deletedAviPath} DCM:{deletedDcmPath}");
                }

                _videoFiles.RemoveAt(_currentIndex);

                if (_videoFiles.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync("영상이 삭제되었습니다.\n 저장된 영상이 존재하지 않아\nScan 화면으로 이동합니다.", CustomMessageWindow.MessageBoxType.AutoClose, 2, CustomMessageWindow.MessageIconType.Info);
                    RequestNavigateToScan?.Invoke();
                    return;
                }

                await CustomMessageWindow.ShowAsync("비디오가 정상적으로 삭제되었습니다.", CustomMessageWindow.MessageBoxType.Ok, 0, CustomMessageWindow.MessageIconType.Info);

                // 마지막 파일 삭제 시 인덱스 보정
                if (_currentIndex >= _videoFiles.Count)
                    _currentIndex = _videoFiles.Count - 1;

                UpdateCurrentFile();

           

            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  AVI 경로 기준으로 DCM 경로 계산
        //
        //  AVI:  VIDEO/박한용_2634/20250313/202503130001/박한용_2634_202503130001_1_Dicom.avi
        //  DCM: DICOM/박한용_2634/20250313/202503130001/Video/박한용_2634_202503130001_1_Dicom.dcm
        //
        //  1. VIDEO 루트 → DICOM 루트 변경
        //  2. 폴더 경로 유지
        //  3. Video 폴더 추가
        //  4. 확장자 .avi → .dcm
        // ═══════════════════════════════════════════
        private async Task<string> GetDicomPathFromAvi(string aviPath)
        {
            try
            {
                string videoRoot = Path.Combine(Common.executablePath, "VIDEO");
                string dicomRoot = Path.Combine(Common.executablePath, "DICOM");

                // VIDEO 루트 기준 상대경로
                string relative = aviPath.Substring(videoRoot.Length)
                                         .TrimStart(Path.DirectorySeparatorChar);

                // 확장자 .avi → .dcm
                string dcmFileName = Path.ChangeExtension(Path.GetFileName(relative), ".dcm");
                string folderRelative = Path.GetDirectoryName(relative);

                return Path.Combine(dicomRoot, folderRelative, "Video", dcmFileName);
            }
            catch (Exception ex) { await Common.WriteLog(ex); return null; }
        }

        #endregion

        #region Comment 저장

        // ═══════════════════════════════════════════
        //  Comment Save
        //  TODO: 코멘트 저장 로직 구현 예정
        // ═══════════════════════════════════════════
        private async Task ExecuteCommentSave()
        {
            try
            {
                // TODO: 코멘트 저장 구현
                await CustomMessageWindow.ShowAsync("저장되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region 배속 제어

        // ═══════════════════════════════════════════
        //  🐢 느리게
        //  1x → x0.5 → x0.25 → x0.16 → 1x (복귀)
        // ═══════════════════════════════════════════
        private async Task ExecuteSlower()
        {
            try
            {
                _fastIndex = 0;
                _slowIndex = (_slowIndex + 1) % _slowSteps.Length;
                ApplySpeed(
                    _slowSteps[_slowIndex],
                    _slowLabels[_slowIndex],
                    _slowIndex == 0 ? SpeedMode.Normal : SpeedMode.Slow);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  🐇 빠르게
        //  1x → x2 → x4 → x6 → 1x (복귀)
        // ═══════════════════════════════════════════
        private async Task ExecuteFaster()
        {
            try
            {
                _slowIndex = 0;
                _fastIndex = (_fastIndex + 1) % _fastSteps.Length;
                ApplySpeed(
                    _fastSteps[_fastIndex],
                    _fastLabels[_fastIndex],
                    _fastIndex == 0 ? SpeedMode.Normal : SpeedMode.Fast);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private enum SpeedMode { Normal, Slow, Fast }

        // ── 배속 적용: SpeedRatio + 라벨 + 색상 한번에 갱신 ──
        private void ApplySpeed(double ratio, string label, SpeedMode mode)
        {
            CurrentSpeedRatio = ratio;
            SpeedLabel = label;
            if (mode == SpeedMode.Slow)
                SpeedLabelColor = SpeedSlowBrush;    // 파란색
            else if (mode == SpeedMode.Fast)
                SpeedLabelColor = SpeedFastBrush;    // 주황색
            else
                SpeedLabelColor = SpeedNormalBrush;  // 흰색
        }

        // ═══════════════════════════════════════════
        //  배속 초기화
        //  이전/다음 영상 이동 시 xaml.cs 에서 호출
        // ═══════════════════════════════════════════
        public async Task ResetSpeed()
        {
            try
            {
                _slowIndex = 0;
                _fastIndex = 0;
                ApplySpeed(1.0, "1x", SpeedMode.Normal);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region 페이지 이동

        // ═══════════════════════════════════════════
        //  BACK → Scan 화면 복귀
        //  xaml.cs 에서 CleanupMedia() 먼저 호출 후 이 커맨드 실행
        // ═══════════════════════════════════════════
        private async Task ExecuteNavigateBack()
        {
            try
            {
                MainPage.Instance.NavigateTo(
                    new Scan_Page.Scan(Patient, StudyId));
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region 헬퍼

        // ── 파일명에서 인덱스 번호 추출 ──
        // 박한용_2634_202503130001_3_Avi → 3 (뒤에서 두 번째)
        private int ExtractIndex(string fileName)
        {
            string[] parts = fileName.Split('_');
            if (parts.Length < 2) return 0;
            return int.TryParse(parts[parts.Length - 2], out int idx) ? idx : 0;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}