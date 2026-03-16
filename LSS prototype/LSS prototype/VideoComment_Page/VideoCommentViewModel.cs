using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
            AppDomain.CurrentDomain.BaseDirectory, "VIDEO",
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
        // 🐢 느리게: 1x → x0.5 → x0.25 → x0.16 → 1x
        // 🐇 빠르게: 1x → x2  → x4   → x6   → 1x
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
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));  // 파란색
        private static readonly SolidColorBrush SpeedFastBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));  // 주황색

        // ── MediaElement 조작 Action (xaml.cs 에서 주입) ──
        private Action _seekBackAction;
        private Action _seekForwardAction;
        private Action _playPauseAction;

        // ═══════════════════════════════════════════
        //  바인딩 프로퍼티
        // ═══════════════════════════════════════════

        // 현재 재생할 파일 경로
        // xaml.cs PropertyChanged 감지 → MediaElement.Source 변경 + 자동 재생
        private string _currentVideoPath;
        public string CurrentVideoPath
        {
            get => _currentVideoPath;
            private set { _currentVideoPath = value; OnPropertyChanged(); }
        }

        // 현재 파일명 표시 (우상단)
        private string _currentFileName;
        public string CurrentFileName
        {
            get => _currentFileName;
            private set { _currentFileName = value; OnPropertyChanged(); }
        }

        // 파일 카운터 표시 "2 / 5"
        private string _fileCounter;
        public string FileCounter
        {
            get => _fileCounter;
            private set { _fileCounter = value; OnPropertyChanged(); }
        }

        // 환자 이름 + 코드
        public string PatientName => $"{_patient.PatientName} ({_patient.PatientCode})";

        // 환자 생년월일 + 성별
        public string PatientInfo => $"{_patient.BirthDate:yyyy-MM-dd} / {_patient.Sex}";

        // Position 콤보박스
        private string _selectedPosition;
        public string SelectedPosition
        {
            get => _selectedPosition;
            set { _selectedPosition = value; OnPropertyChanged(); }
        }

        // Anatomical 콤보박스
        private string _selectedAnatomical;
        public string SelectedAnatomical
        {
            get => _selectedAnatomical;
            set { _selectedAnatomical = value; OnPropertyChanged(); }
        }

        // Comment 텍스트
        private string _commentText;
        public string CommentText
        {
            get => _commentText;
            set { _commentText = value; OnPropertyChanged(); }
        }

        // ── 배속 라벨 ("1x" / "x0.5" / "x0.25" / "x0.16" / "x2" / "x4" / "x6") ──
        private string _speedLabel = "1x";
        public string SpeedLabel
        {
            get => _speedLabel;
            private set { _speedLabel = value; OnPropertyChanged(); }
        }

        // ── 배속 표시 색상 ──
        // 1x → 흰색 / 느림 → 파란색 / 빠름 → 주황색
        private SolidColorBrush _speedLabelColor = SpeedNormalBrush;
        public SolidColorBrush SpeedLabelColor
        {
            get => _speedLabelColor;
            private set { _speedLabelColor = value; OnPropertyChanged(); }
        }

        // ── 현재 배속 값 ──
        // xaml.cs PropertyChanged 감지 → MediaElement.SpeedRatio 적용
        private double _currentSpeedRatio = 1.0;
        public double CurrentSpeedRatio
        {
            get => _currentSpeedRatio;
            private set { _currentSpeedRatio = value; OnPropertyChanged(); }
        }

        // ── 재생/정지 아이콘 ("▶" / "⏸") ──
        private string _playPauseIcon = "▶";
        public string PlayPauseIcon
        {
            get => _playPauseIcon;
            set { _playPauseIcon = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════
        //  커맨드
        // ═══════════════════════════════════════════
        public ICommand VideoDeleteCommand { get; }
        public ICommand CommentSaveCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand SlowerCommand { get; }        // 🐢 느리게
        public ICommand FasterCommand { get; }        // 🐇 빠르게
        public ICommand SeekBackCommand { get; }        // 1초 전
        public ICommand SeekForwardCommand { get; }        // 1초 후
        public ICommand PlayPauseCommand { get; }        // 재생/정지
        public ICommand NavigateBackCommand { get; }        // BACK → Scan 복귀

        // ═══════════════════════════════════════════
        //  생성자
        // ═══════════════════════════════════════════
        public VideoCommentViewModel(PatientModel patient, string studyId)
        {
            _patient = patient;
            _studyId = studyId;
            Patient = patient;
            StudyId = studyId;

            VideoDeleteCommand = new RelayCommand(_ => ExecuteVideoDelete());
            CommentSaveCommand = new RelayCommand(_ => ExecuteCommentSave());
            ExitCommand = new RelayCommand(Common.ExcuteExit);
            SlowerCommand = new RelayCommand(_ => ExecuteSlower());
            FasterCommand = new RelayCommand(_ => ExecuteFaster());

            // Action 은 xaml.cs SetMediaActions() 로 주입받음
            // null 체크 후 호출하므로 주입 전 클릭해도 안전
            SeekBackCommand = new RelayCommand(_ => _seekBackAction?.Invoke());
            SeekForwardCommand = new RelayCommand(_ => _seekForwardAction?.Invoke());
            PlayPauseCommand = new RelayCommand(_ => _playPauseAction?.Invoke());

            NavigateBackCommand = new RelayCommand(_ => ExecuteNavigateBack());
        }

        // ═══════════════════════════════════════════
        //  MediaElement 조작 Action 주입
        //  xaml.cs OnLoaded 에서 호출
        // ═══════════════════════════════════════════
        public void SetMediaActions(
            Action seekBack,
            Action seekForward,
            Action playPause)
        {
            _seekBackAction = seekBack;
            _seekForwardAction = seekForward;
            _playPauseAction = playPause;
        }

        // ═══════════════════════════════════════════
        //  초기화 - VIDEO/ 폴더에서 AVI 파일 목록 수집
        //  xaml.cs Loaded 에서 호출
        //  반환값: 파일 있으면 true, 없으면 false
        // ═══════════════════════════════════════════
        public bool Initialize()
        {
            try
            {
                if (!Directory.Exists(VideoDir))
                {
                    CustomMessageWindow.Show("재생할 영상 파일이 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                // Del_ 파일 제외 + 번호순 정렬
                _videoFiles = Directory.GetFiles(VideoDir, "*.avi")
                    .Where(f => !Path.GetFileName(f).StartsWith("Del_"))
                    .OrderBy(f => ExtractIndex(Path.GetFileNameWithoutExtension(f)))
                    .ToList();

                if (_videoFiles.Count == 0)
                {
                    CustomMessageWindow.Show("재생할 영상 파일이 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _currentIndex = 0;
                UpdateCurrentFile();
                return true;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        // ═══════════════════════════════════════════
        //  이전 / 다음 파일 이동
        //  UpdateCurrentFile() → CurrentVideoPath 변경
        //  → xaml.cs PropertyChanged 감지 → 자동 재생
        // ═══════════════════════════════════════════
        public bool MovePrev()
        {
            try
            {
                if (_currentIndex <= 0)
                {
                    CustomMessageWindow.Show("첫 번째 영상입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    return false;
                }
                  

                _currentIndex--;
                UpdateCurrentFile();
                return true;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        public bool MoveNext()
        {
            try
            {
                if (_currentIndex >= _videoFiles.Count - 1)
                {
                    CustomMessageWindow.Show("마지막 영상입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    return false;   
                }

                   

                _currentIndex++;
                UpdateCurrentFile();
                return true;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        // ═══════════════════════════════════════════
        //  CurrentVideoPath / FileName / Counter 갱신
        //  CurrentVideoPath 변경 → xaml.cs 트리거 → 자동 재생
        // ═══════════════════════════════════════════
        private void UpdateCurrentFile()
        {
            if (_videoFiles.Count == 0) return;

            CurrentVideoPath = _videoFiles[_currentIndex];
            CurrentFileName = Path.GetFileNameWithoutExtension(_videoFiles[_currentIndex]);
            FileCounter = $"{_currentIndex + 1} / {_videoFiles.Count}";
        }

        // ═══════════════════════════════════════════
        //  영상 삭제
        //  확인 팝업 → CurrentVideoPath = null (xaml.cs StopAndReset 트리거)
        //  → Del_ 접두사 추가 → 목록 갱신
        //  TODO: DELETE_TB INSERT
        // ═══════════════════════════════════════════
        private void ExecuteVideoDelete()
        {
            try
            {
                if (_videoFiles.Count == 0) return;

                var result = CustomMessageWindow.Show(
                    "영상을 삭제하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Warning);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                string currentFile = _videoFiles[_currentIndex];
                string dir = Path.GetDirectoryName(currentFile);
                string fileName = Path.GetFileName(currentFile);
                string deletedPath = Path.Combine(dir, "Del_" + fileName);

                // MediaElement 가 파일을 열고 있으므로
                // CurrentVideoPath = null → xaml.cs StopAndReset() → 파일 잠금 해제
                CurrentVideoPath = null;

                File.Move(currentFile, deletedPath);

                // TODO: DELETE_TB INSERT

                _videoFiles.RemoveAt(_currentIndex);

                if (_videoFiles.Count == 0)
                {
                    CurrentFileName = "";
                    FileCounter = "0 / 0";
                    return;
                }

                // 삭제 후 인덱스 보정 (마지막 파일 삭제 시)
                if (_currentIndex >= _videoFiles.Count)
                    _currentIndex = _videoFiles.Count - 1;

                UpdateCurrentFile();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  Comment Save
        //  TODO: 코멘트 저장 로직 구현 예정
        // ═══════════════════════════════════════════
        private void ExecuteCommentSave()
        {
            try
            {
                // TODO: 코멘트 저장 구현
                CustomMessageWindow.Show("저장되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  배속 제어
        //
        //  🐢 SlowerCommand
        //    1x → x0.5 → x0.25 → x0.16 → 1x (복귀)
        //    fast 상태면 먼저 1x 리셋 후 slow 진입
        //
        //  🐇 FasterCommand
        //    1x → x2 → x4 → x6 → 1x (복귀)
        //    slow 상태면 먼저 1x 리셋 후 fast 진입
        //
        //  CurrentSpeedRatio 변경 → xaml.cs PropertyChanged 감지
        //  → MediaElement.SpeedRatio 적용
        // ═══════════════════════════════════════════
        private void ExecuteSlower()
        {
            try
            {
                _fastIndex = 0;

                // 마지막 단계(x0.16) 에서 한 번 더 누르면 → 0(1x) 으로 복귀
                _slowIndex = (_slowIndex + 1) % _slowSteps.Length;

                ApplySpeed(
                    _slowSteps[_slowIndex],
                    _slowLabels[_slowIndex],
                    _slowIndex == 0 ? SpeedMode.Normal : SpeedMode.Slow
                );
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void ExecuteFaster()
        {
            try
            {
                _slowIndex = 0;

                // 마지막 단계(x6) 에서 한 번 더 누르면 → 0(1x) 으로 복귀
                _fastIndex = (_fastIndex + 1) % _fastSteps.Length;

                ApplySpeed(
                    _fastSteps[_fastIndex],
                    _fastLabels[_fastIndex],
                    _fastIndex == 0 ? SpeedMode.Normal : SpeedMode.Fast
                );
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ── 배속 모드 ──
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
        //  터치로 이전/다음 영상 이동 시 xaml.cs 에서 호출
        //  slow / fast 인덱스 모두 0 으로 리셋 → 1x 복귀
        // ═══════════════════════════════════════════
        public void ResetSpeed()
        {
            try
            {
                _slowIndex = 0;
                _fastIndex = 0;
                ApplySpeed(1.0, "1x", SpeedMode.Normal);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  BACK → Scan 화면 복귀
        //  xaml.cs 에서 CleanupMedia() 먼저 호출 후 이 커맨드 실행
        // ═══════════════════════════════════════════
        private void ExecuteNavigateBack()
        {
            try
            {
                MainPage.Instance.NavigateTo(
                    new Scan_Page.Scan(Patient, StudyId));
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ── 파일명에서 인덱스 번호 추출 ──
        // 박한용_2634_202503130001_3_Avi → 3
        private int ExtractIndex(string fileName)
        {
            string[] parts = fileName.Split('_');
            if (parts.Length < 2) return 0;
            return int.TryParse(parts[parts.Length - 2], out int idx) ? idx : 0;
        }

        // ═══════════════════════════════════════════
        //  INotifyPropertyChanged
        // ═══════════════════════════════════════════
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}