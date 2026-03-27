using FellowOakDicom;
using FellowOakDicom.Imaging;
using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Ink;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LSS_prototype.DB_CRUD;
using System.Threading.Tasks;
using LSS_prototype.Auth;
using LSS_prototype.Login_Page;
using System.Windows;

namespace LSS_prototype.ImageComment_Page
{
    public class ImageCommentViewModel : INotifyPropertyChanged
    {
        #region 경로 상수

        private string DicomDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
        private string IsfDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ISF");

        /// <summary>
        /// DCM 경로 → ISF 경로 변환
        /// DICOM/.../Image/파일.dcm → ISF/.../파일.isf
        /// </summary>
        private string GetIsfPath(string dcmPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(dcmPath);
            string studyDir = Path.GetDirectoryName(Path.GetDirectoryName(dcmPath));
            string relative = studyDir.Substring(DicomDir.Length).TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(IsfDir, relative, fileName + ".isf");
        }

        #endregion

        #region 이벤트

        /// <summary>파일 전부 삭제 시 코드비하인드에 Scan 화면 이동 신호</summary>
        public event Action RequestNavigateToScan;

        /// <summary>SAVE 버튼 → 코드비하인드에서 DrawingCanvas 접근 필요하므로 이벤트로 위임</summary>
        public event Func<Task> RequestSave;

        #endregion

        #region 내부 상태

        private List<string> _dcmFiles = new List<string>();
        private int _currentIndex = 0;

        /// <summary>
        /// 변경 감지 플래그
        /// IsCommentDirty → CommentText setter에서 자동 세팅
        /// _isDirty       → 코드비하인드에서 ISF 드로잉 변경 시 세팅
        /// 둘 중 하나라도 true면 페이지 이동/나가기 시 저장 팝업
        /// </summary>
        public bool IsCommentDirty { get; private set; } = false;

        #endregion

        #region 바인딩 프로퍼티

        private string _pageIndicator;
        public string PageIndicator
        {
            get => _pageIndicator;
            private set { _pageIndicator = value; OnPropertyChanged(); }
        }

        private WriteableBitmap _currentImage;
        public WriteableBitmap CurrentImage
        {
            get => _currentImage;
            set { _currentImage = value; OnPropertyChanged(); }
        }

        private StrokeCollection _currentStrokes;
        public StrokeCollection CurrentStrokes
        {
            get => _currentStrokes;
            set { _currentStrokes = value; OnPropertyChanged(); }
        }

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

        /// <summary>
        /// CommentText setter에서 IsCommentDirty 자동 세팅
        ///   LoadPage / Reset 에서는 필드 직접 할당 후 OnPropertyChanged 호출
        ///   → setter 통하면 IsCommentDirty=true 되므로 반드시 이 방식 사용
        /// </summary>
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

        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set { _selectedPatient = value; OnPropertyChanged(); }
        }

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

        public ICommand ImageDeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ToggleMenuCommand { get; }
        public ICommand LockCommand { get; }
        public ICommand LogoutCommand { get; }

        #endregion

        #region 생성자

        public ImageCommentViewModel(PatientModel selectedPatient, string studyId = null)
        {
            SelectedPatient = selectedPatient;
            ImageDeleteCommand = new RelayCommand(async _ => await ExecuteImageDelete());
            ExitCommand = new RelayCommand(async _ => await Common.ExcuteExit());
            LockCommand = new AsyncRelayCommand(async _ => await ExecuteLock());
            LogoutCommand = new AsyncRelayCommand(async _ => await ExecuteLogout());
            ToggleMenuCommand = new RelayCommand(_ => ToggleMenu());

            SaveCommand = new AsyncRelayCommand(async _ =>
            {
                bool save = await ConfirmSaveAll();
                if (save && RequestSave != null)
                    await RequestSave.Invoke();
            });
        }

        #endregion

        #region 초기화

        /// <summary>
        /// DCM 파일 목록 로드 및 마지막 이미지 표시
        /// </summary>
        public async Task<bool> Initialize(PatientModel patient, string studyId)
        {
            try
            {
                if (patient == null || string.IsNullOrWhiteSpace(studyId))
                {
                    await CustomMessageWindow.ShowAsync(
                        "환자 정보 또는 Study 정보가 올바르지 않습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                string patientFolder = $"{patient.PatientName}_{patient.PatientCode}";
                string studyDateFolder = studyId.Substring(0, 8);
                string imageDir = Path.Combine(DicomDir, patientFolder, studyDateFolder, studyId, "Image");
                string isfDir = Path.Combine(IsfDir, patientFolder, studyDateFolder, studyId);
                Directory.CreateDirectory(isfDir);

                if (!Directory.Exists(imageDir))
                {
                    await CustomMessageWindow.ShowAsync(
                        "해당 세션의 DICOM 폴더가 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _dcmFiles = Directory.EnumerateFiles(imageDir, "*.dcm")
                    .Where(f => !Path.GetFileName(f).StartsWith("Del_"))
                    .OrderBy(f =>
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        string last = name.Split('_').Last();
                        return int.TryParse(last, out int n) ? n : int.MaxValue;
                    })
                    .ToList();

                if (_dcmFiles.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync(
                        "저장된 이미지가 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _currentIndex = _dcmFiles.Count - 1;
                LoadPage(_currentIndex);
                return true;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        #endregion

        #region 페이지 로드 / 이동

        /// <summary>
        /// DCM 이미지 + ISF 드로잉 + DICOM Comment 태그 로드
        /// CommentText 로드 시 필드 직접 할당 → setter 경유 시 IsCommentDirty=true 방지
        /// </summary>
        private void LoadPage(int index)
        {
            string dcmPath = _dcmFiles[index];
            string isfPath = GetIsfPath(dcmPath);

            // DCM → WriteableBitmap
            var dicomFile = DicomFile.Open(dcmPath);
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
            CurrentImage = bitmap;

            // ISF 로드 (없으면 빈 StrokeCollection)
            CurrentStrokes = File.Exists(isfPath)
                ? LoadStrokesFromFile(isfPath)
                : new StrokeCollection();

            // DICOM Image Comments 태그 로드
            //   setter 통하면 IsCommentDirty=true 되므로 필드 직접 할당
            _commentText = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.ImageComments, string.Empty);
            OnPropertyChanged(nameof(CommentText));

            IsCommentDirty = false;
            PageIndicator = $"{index + 1:D2}/{_dcmFiles.Count:D2}";
        }

        private StrokeCollection LoadStrokesFromFile(string isfPath)
        {
            using (var fs = File.OpenRead(isfPath))
                return new StrokeCollection(fs);
        }

        /// <summary>페이지 이동 가능 여부 (false면 코드비하인드에서 바로 리턴)</summary>
        public bool CanNavigate(bool goNext)
        {
            int targetIndex = goNext ? _currentIndex + 1 : _currentIndex - 1;
            return targetIndex >= 0 && targetIndex < _dcmFiles.Count;
        }

        public async Task<bool> NavigatePage(bool goNext)
        {
            try
            {
                int targetIndex = goNext ? _currentIndex + 1 : _currentIndex - 1;
                if (targetIndex < 0 || targetIndex >= _dcmFiles.Count)
                    return false;

                _currentIndex = targetIndex;
                LoadPage(_currentIndex);
                return true;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        #endregion

        #region 저장

        /// <summary>
        /// ISF + DICOM Comment 통합 저장
        /// 코드비하인드에서 DrawingCanvas.Strokes 를 넘겨줌
        /// 1. ISF 파일 저장 (드로잉 없으면 삭제)
        /// 2. DICOM Image Comments 태그 저장
        /// 3. IsCommentDirty 리셋 (_isDirty 는 코드비하인드에서 리셋)
        /// </summary>
        public async Task<bool> SaveComment(StrokeCollection strokes, double canvasWidth, double canvasHeight)
        {
            try
            {
                string dcmPath = _dcmFiles[_currentIndex];
                string isfPath = GetIsfPath(dcmPath);

                // ── ISF 저장 ──
                if (strokes == null || strokes.Count == 0)
                {
                    if (File.Exists(isfPath)) File.Delete(isfPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(isfPath));

                    // ISF 안에 캔버스 크기 저장 (복구창 스케일 변환용)
                    var guidWidth = new Guid("A1B2C3D4-0001-0002-0003-000000000001");
                    var guidHeight = new Guid("A1B2C3D4-0001-0002-0003-000000000002");
                    strokes.AddPropertyData(guidWidth, canvasWidth.ToString());
                    strokes.AddPropertyData(guidHeight, canvasHeight.ToString());

                    using (var fs = File.Create(isfPath))
                        strokes.Save(fs);
                }

                // ── DICOM Image Comments 태그 저장 ──
                var dicomFile = DicomFile.Open(dcmPath, FileReadOption.ReadAll);

                if (string.IsNullOrWhiteSpace(CommentText))
                    dicomFile.Dataset.Remove(DicomTag.ImageComments);
                else
                    dicomFile.Dataset.AddOrUpdate(DicomTag.ImageComments, CommentText);

                // temp 파일로 먼저 저장 후 원본 교체 (파일 손상 방지)
                string tempPath = dcmPath + ".tmp";
                dicomFile.Save(tempPath);
                File.Delete(dcmPath);
                File.Move(tempPath, dcmPath);

                // ── DB COMMENT TB UPSERT ──
                var db = new DB_Manager();
                db.UpsertComment("IMAGE", Path.GetFileNameWithoutExtension(dcmPath), CommentText ?? string.Empty);

                IsCommentDirty = false;
                // _isDirty 는 코드비하인드에서 리셋
                return true;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        /// <summary>
        /// 저장 여부 확인 팝업 (ISF + Comment 통합)
        /// 페이지 이동 / 나가기 / SAVE 버튼 공통 사용
        /// </summary>
        public async Task<bool> ConfirmSaveAll()
        {
            try
            {
                var result = await CustomMessageWindow.ShowAsync(
                    "코멘트 정보를 저장하시겠습니까?",
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

        #region 삭제

        private async Task ExecuteImageDelete()
        {
            try
            {
                var result = await CustomMessageWindow.ShowAsync(
                    "이미지를 삭제하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Warning);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                string dcmPath = _dcmFiles[_currentIndex];
                string dcmDir = Path.GetDirectoryName(dcmPath);
                string dcmFileName = Path.GetFileName(dcmPath);
                string newDcmPath = Path.Combine(dcmDir, "Del_" + dcmFileName);
                File.Move(dcmPath, newDcmPath);

                string isfPath = GetIsfPath(dcmPath);
                if (File.Exists(isfPath))
                {
                    string isfDir = Path.GetDirectoryName(isfPath);
                    string isfFileName = Path.GetFileName(isfPath);
                    File.Move(isfPath, Path.Combine(isfDir, "Del_" + isfFileName));
                }

                var db = new DB_Manager();
                db.InsertImageDeleteLog(newDcmPath, SelectedPatient.PatientCode, SelectedPatient.PatientName);
                Common.WriteSessionLog(
                    $"[IMAGE DELETE] User:{Common.CurrentUserId} " +
                    $"PatientCode:{SelectedPatient.PatientCode} " +
                    $"Patient_Name:{SelectedPatient.PatientName} File:{newDcmPath}");

                _dcmFiles.RemoveAt(_currentIndex);

                if (_dcmFiles.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync(
                        "이미지가 삭제되었습니다. \n 저장된 이미지가 존재하지 않아\nScan 화면으로 이동합니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 0,
                        CustomMessageWindow.MessageIconType.Info);
                    RequestNavigateToScan?.Invoke();
                    return;
                }

                await CustomMessageWindow.ShowAsync(
                    "이미지가 정상적으로 삭제되었습니다.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Info);

                _currentIndex = Math.Min(_currentIndex, _dcmFiles.Count - 1);
                LoadPage(_currentIndex);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region 세션 (Lock / Logout)

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

        #endregion

        #region 리셋

        /// <summary>
        /// 입력값 초기화
        /// CommentText는 필드 직접 할당 → IsCommentDirty=true 방지
        /// </summary>
        public void Reset()
        {
            SelectedPosition = null;
            SelectedAnatomical = null;

            _commentText = string.Empty;
            OnPropertyChanged(nameof(CommentText));
            IsCommentDirty = false;
        }

        /// <summary>
        /// IsCommentDirty 리셋
        /// 저장 완료 또는 RESET 버튼 후 코드비하인드에서 호출
        /// _isDirty 는 코드비하인드 소유이므로 거기서 직접 false 처리
        /// </summary>
        public void ResetDirty()
        {
            IsCommentDirty = false;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}