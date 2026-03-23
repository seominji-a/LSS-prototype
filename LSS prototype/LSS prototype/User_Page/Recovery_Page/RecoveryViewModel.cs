using FellowOakDicom;
using FellowOakDicom.Imaging;
using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LSS_prototype.User_Page
{
    class RecoveryViewModel : INotifyPropertyChanged
    {
        // ── 상수 ──
        private const int EXPIRE_HOURS = 72;    // 72시간 후 만료
        private const int PREVIEW_DELAY = 1000; // 미리보기 딜레이 1초

        // ── DB 원본 데이터 ──
        private List<RecoveryModel> _allLogs = new List<RecoveryModel>();

        // ── 미리보기 딜레이용 취소 토큰 ──
        private CancellationTokenSource _previewCts;

        // ── 검색 딜레이 (Patient 와 동일한 SearchDebouncer 사용) ──
        private readonly SearchDebouncer _searchDebouncer;

        #region 바인딩 프로퍼티

        // ── ISF 이미지 크기 (스케일 변환용) ──
        public double PreviewImageWidth { get; private set; }
        public double PreviewImageHeight { get; private set; }

        // ── ISF 드로잉 데이터 ──
        private StrokeCollection _currentStrokes;
        public StrokeCollection CurrentStrokes
        {
            get => _currentStrokes;
            set
            {
                _currentStrokes = value;
                OnPropertyChanged();
            }
        }

        // ── 필터 콤보박스 항목 ──
        public List<string> FilterOptions { get; } = new List<string>
        {
            "ALL", "IMAGE", "DICOM_VIDEO", "NORMAL_VIDEO"
        };

        // ── 화면에 표시되는 필터링된 목록 ──
        private ObservableCollection<RecoveryModel> _filteredLogs;
        public ObservableCollection<RecoveryModel> FilteredLogs
        {
            get => _filteredLogs;
            set
            {
                _filteredLogs = value;
                OnPropertyChanged();
            }
        }

        // ── 선택된 행 ──
        // 행 클릭 = 미리보기 트리거
        // 체크박스 = 복구/강제삭제 작업 대상 선택
        private RecoveryModel _selectedLog;
        public RecoveryModel SelectedLog
        {
            get => _selectedLog;
            set
            {
                _selectedLog = value;
                OnPropertyChanged();

                _previewCts?.Cancel();
                _previewCts = new CancellationTokenSource();
                _ = LoadPreviewAsync(_previewCts.Token);
            }
        }

        // ── FILE_TYPE 필터 ──
        private string _selectedFilter = "ALL";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                _selectedFilter = value;
                OnPropertyChanged();
                _ = ApplyFilter(); // UI 반응 대기가 목적이라 딱히 동기 처리안해도됨 
            }
        }

        // ── 검색어 ──
        // Patient와 동일하게:
        // setter에서는 값만 반영
        // 실제 검색 호출은 OnSearchTextChanged() 에서 처리
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
            }
        }

        // ── 뷰어 상태 ──
        private bool _isViewerEmpty = true;
        public bool IsViewerEmpty
        {
            get => _isViewerEmpty;
            set
            {
                _isViewerEmpty = value;
                OnPropertyChanged();
            }
        }

        private bool _isImageVisible;
        public bool IsImageVisible
        {
            get => _isImageVisible;
            set
            {
                _isImageVisible = value;
                OnPropertyChanged();
            }
        }

        private bool _isVideoVisible;
        public bool IsVideoVisible
        {
            get => _isVideoVisible;
            set
            {
                _isVideoVisible = value;
                OnPropertyChanged();
            }
        }

        // ── 뷰어 데이터 ──
        private WriteableBitmap _previewImageSource;
        public WriteableBitmap PreviewImageSource
        {
            get => _previewImageSource;
            set
            {
                _previewImageSource = value;
                OnPropertyChanged();
            }
        }

        // AVI 파일 경로 - xaml.cs 에서 PropertyChanged 감지 → MediaElement 재생
        private string _previewVideoPath;
        public string PreviewVideoPath
        {
            get => _previewVideoPath;
            set
            {
                _previewVideoPath = value;
                OnPropertyChanged();
            }
        }

        private string _selectedFilePath;
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                _selectedFilePath = value;
                OnPropertyChanged();
            }
        }

        private string _selectedFileType;
        public string SelectedFileType
        {
            get => _selectedFileType;
            set
            {
                _selectedFileType = value;
                OnPropertyChanged();
            }
        }

        // ISF 저장 당시 캔버스 크기
        public double OriginalCanvasWidth { get; private set; }
        public double OriginalCanvasHeight { get; private set; }

        #endregion

        #region 커맨드

        public ICommand NavigateBackCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand RecoverCommand { get; }
        public ICommand ForceDeleteCommand { get; }

        #endregion

        #region 생성자

        public RecoveryViewModel()
        {
            NavigateBackCommand = new RelayCommand(_ =>
                MainPage.Instance.NavigateTo(new User()));

            ExitCommand = new RelayCommand(async _=> await Common.ExcuteExit());
            RecoverCommand = new RelayCommand(async _ => await ExecuteRecover());
            ForceDeleteCommand = new RelayCommand(async _ => await ExecuteForceDelete());

            // Patient 와 동일하게 SearchDebouncer 초기화
            _searchDebouncer = new SearchDebouncer(async keyword => await ExecuteSearch(keyword), delayMs: 500);
        }

        public async Task InitializeAsync()
        {
            await LoadLogs();
        }

        // Patient 와 동일하게 Dispose 구현
        public void Dispose()
        {
            _searchDebouncer?.Dispose();
        }

        #endregion

        #region 데이터 로드

        private async Task LoadLogs()
        {
            try
            {
                var db = new DB_Manager();
                var logs = db.GetDeleteLogs();

                foreach (var log in logs)
                {
                    if (DateTime.TryParse(log.DeletedAt, out DateTime deletedAt))
                    {
                        DateTime expireAt = deletedAt.AddHours(EXPIRE_HOURS);
                        TimeSpan remain = expireAt - DateTime.Now;

                        if (remain.TotalSeconds <= 0)
                        {
                            log.IsExpired = true;
                            log.RemainText = "만료";
                        }
                        else
                        {
                            log.IsExpired = false;
                            log.RemainText = $"{(int)remain.TotalHours}시간 {remain.Minutes}분";
                        }
                    }

                    if (log.IsRecovered == "Y") log.RemainText = "복구처리";
                    if (log.IsForceDeleted == "Y") log.RemainText = "강제삭제";
                    if (log.PatientDeleted == "Y" && log.FileType != "PATIENT") log.RemainText = "환자 삭제";

                    log.IsChecked = false;
                }

                _allLogs = logs;
                await ApplyFilter();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task UpdateItemInPlace(RecoveryModel log, bool isRecover)
        {
            if (isRecover)
            {
                log.IsRecovered = "Y";
                log.RemainText = "복구처리";
            }
            else
            {
                log.IsForceDeleted = "Y";
                log.RemainText = "강제삭제";
            }

            log.IsChecked = false;

            if (_selectedLog?.DeleteId == log.DeleteId)
                await ResetViewer();
        }

        #endregion

        #region 필터 / 검색

        // ── Patient 와 동일한 흐름 ──
        // xaml.cs TextChanged → OnSearchTextChanged() → SearchDebouncer → 0.5초 후 ExecuteSearch()
        public void OnSearchTextChanged(string text)
        {
            SearchText = text;
            _searchDebouncer.OnTextChanged(text);
        }

        // 필터 콤보 변경 시 현재 검색어 기준 즉시 재실행
        private async Task ApplyFilter()
        {
            await ExecuteSearch(SearchText);
        }

        private async Task ExecuteSearch(string keyword)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    int? selectedId = SelectedLog?.DeleteId;

                    IEnumerable<RecoveryModel> source = _allLogs;

                    // FILE_TYPE 필터 먼저 적용
                    if (SelectedFilter != "ALL")
                        source = source.Where(x => x.FileType == SelectedFilter);

                    if (string.IsNullOrWhiteSpace(keyword))
                    {
                        // 검색어 없으면 현재 필터 기준 원래 목록 복원
                        FilteredLogs = new ObservableCollection<RecoveryModel>(source);

                        // 검색어 지울 땐 선택 해제 (Patient와 동일)
                        SelectedLog = null;
                        return;
                    }

                    string kwNoSpace = keyword.Replace(" ", "");

                    FilteredLogs = new ObservableCollection<RecoveryModel>(
                        source.Where(x => MatchesKeyword(x, keyword, kwNoSpace))
                    );

                    // 검색 중일 땐 기존 선택 유지
                    if (selectedId.HasValue)
                        SelectedLog = FilteredLogs.FirstOrDefault(x => x.DeleteId == selectedId.Value);
                });
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private bool MatchesKeyword(RecoveryModel x, string keyword, string kwNoSpace)
        {
            // Patient와 동일한 방식
            string nameNoSpace = (x.DisplayName ?? "").Replace(" ", "");

            return
                nameNoSpace.IndexOf(kwNoSpace, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (x.PatientName ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.PatientCode.ToString().Contains(keyword);
        }

        #endregion

        #region 미리보기

        private async Task LoadPreviewAsync(CancellationToken ct)
        {
            try
            {
                await ResetViewer();

                if (_selectedLog == null) return;
                if (_selectedLog.IsExpired) return;
                if (_selectedLog.IsRecovered == "Y") return;
                if (_selectedLog.IsForceDeleted == "Y") return;
                if (_selectedLog.PatientDeleted == "Y" && _selectedLog.FileType != "PATIENT") return; 

                await Task.Delay(PREVIEW_DELAY, ct);
                if (ct.IsCancellationRequested) return;

                switch (_selectedLog.FileType)
                {
                    case "IMAGE":
                        await LoadDicomPreviewAsync(_selectedLog.ImagePath, ct);
                        break;

                    case "DICOM_VIDEO":
                    case "NORMAL_VIDEO":
                        await LoadVideoPreview(_selectedLog.AviPath);
                        break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task ResetViewer()
        {
            try
            {
                IsViewerEmpty = true;
                IsImageVisible = false;
                IsVideoVisible = false;
                PreviewImageSource = null;
                PreviewVideoPath = null;
                SelectedFilePath = null;
                SelectedFileType = null;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task LoadDicomPreviewAsync(string dcmPath, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(dcmPath) || !File.Exists(dcmPath)) return;

                var bitmap = await Task.Run(() =>
                {
                    var dicomFile = DicomFile.Open(dcmPath);
                    var dicomImage = new DicomImage(dicomFile.Dataset);
                    var rendered = dicomImage.RenderImage();
                    var pixels = rendered.As<byte[]>();

                    WriteableBitmap wb = null;
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        wb = new WriteableBitmap(
                            rendered.Width, rendered.Height, 96, 96,
                            System.Windows.Media.PixelFormats.Bgra32, null);

                        wb.WritePixels(
                            new System.Windows.Int32Rect(0, 0, rendered.Width, rendered.Height),
                            pixels, rendered.Width * 4, 0);

                        wb.Freeze();
                    });

                    dicomImage = null;
                    dicomFile = null;
                    return wb;
                }, ct);

                if (ct.IsCancellationRequested) return;

                PreviewImageWidth = bitmap.PixelWidth;
                PreviewImageHeight = bitmap.PixelHeight;
                SelectedFilePath = dcmPath;
                SelectedFileType = "IMAGE";
                PreviewImageSource = bitmap;
                IsImageVisible = true;
                IsViewerEmpty = false;

                await LoadIsfStrokes (dcmPath);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task LoadVideoPreview(string aviPath)
        {
            try
            {
                if (string.IsNullOrEmpty(aviPath) || !File.Exists(aviPath)) return;

                SelectedFilePath = aviPath;
                SelectedFileType = _selectedLog.FileType;
                PreviewVideoPath = aviPath;
                IsVideoVisible = true;
                IsViewerEmpty = false;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task LoadIsfStrokes(string dcmPath)
        {
            try
            {
                string dicomDir = Path.Combine(Common.executablePath, "DICOM");
                string isfDir = Path.Combine(Common.executablePath, "ISF");

                string fileName = Path.GetFileNameWithoutExtension(dcmPath);
                string studyDir = Path.GetDirectoryName(Path.GetDirectoryName(dcmPath));
                string relative = studyDir.Substring(dicomDir.Length)
                                          .TrimStart(Path.DirectorySeparatorChar);
                string isfPath = Path.Combine(isfDir, relative, fileName + ".isf");

                if (!File.Exists(isfPath))
                {
                    CurrentStrokes = new StrokeCollection();
                    return;
                }

                StrokeCollection strokes;
                using (var fs = File.OpenRead(isfPath))
                    strokes = new StrokeCollection(fs);

                var guidWidth = new Guid("A1B2C3D4-0001-0002-0003-000000000001");
                var guidHeight = new Guid("A1B2C3D4-0001-0002-0003-000000000002");

                OriginalCanvasWidth = strokes.ContainsPropertyData(guidWidth)
                    ? double.Parse(strokes.GetPropertyData(guidWidth).ToString())
                    : 1465;

                OriginalCanvasHeight = strokes.ContainsPropertyData(guidHeight)
                    ? double.Parse(strokes.GetPropertyData(guidHeight).ToString())
                    : 1060;

                CurrentStrokes = strokes;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                CurrentStrokes = new StrokeCollection();
            }
        }

        #endregion

        #region 복구 실행

        private async Task ExecuteRecover()
        {
            try
            {
                var targets = FilteredLogs?
                        .Where(x => x.IsChecked)
                        .OrderBy(x => x.FileType == "PATIENT" ? 1 : 0)
                        .ToList(); // 환자와 환자에 엮인 비디오 및 영상을 다중 선택 후 강제 삭제 했을때를 대비 -> Patient는 무조건 리스트 맨 뒤로 처리 

                if (targets == null || targets.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync("복구할 항목을 선택해주세요.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                var confirm = await CustomMessageWindow.ShowAsync(
                    $"{targets.Count}개 항목을 복구하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Info);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                await ResetViewer();

                var db = new DB_Manager();

                foreach (var log in targets)
                {
                    var renamedFiles = new List<(string From, string To)>();

                    try
                    {
                        switch (log.FileType)
                        {
                            case "IMAGE":
                                await RestoreFile(log.ImagePath, renamedFiles);
                                await RestoreIsfFile(log.ImagePath, renamedFiles);
                                break;

                            case "DICOM_VIDEO":
                                await RestoreFile(log.AviPath, renamedFiles);
                                await RestoreFile(log.DicomPath, renamedFiles);
                                break;

                            case "NORMAL_VIDEO":
                                await RestoreFile(log.AviPath, renamedFiles);
                                break;

                            case "PATIENT":
                                if (!db.RecoverPatientWithLog(log.DeleteId, log.PatientCode, log.PatientName))
                                    break;

                                Common.WriteSessionLog(
                                    $"[PATIENT RECOVER] User:{Common.CurrentUserId} " +
                                    $"Patient:{log.PatientName}({log.PatientCode})");

                                // ✅ 환자 복구 후 전체 새로고침
                                // → 같은 환자의 이미지/영상 행들 PatientDeleted = 'N' 으로 반영
                                await LoadLogs();
                                break;
                        }

                        // ✅ PATIENT 는 RecoverPatientWithLog 내부에서 이미 처리했으니 스킵
                        if (log.FileType != "PATIENT")
                            db.UpdateRecovered(log.DeleteId);

                        switch (log.FileType)
                        {
                            case "IMAGE":
                                Common.WriteSessionLog(
                                    $"[IMAGE RECOVER] User:{Common.CurrentUserId} " +
                                    $"Patient:{log.PatientName}({log.PatientCode}) " +
                                    $"File:{log.ImagePath}");
                                break;

                            case "NORMAL_VIDEO":
                                Common.WriteSessionLog(
                                    $"[NORMAL VIDEO RECOVER] User:{Common.CurrentUserId} " +
                                    $"Patient:{log.PatientName}({log.PatientCode}) " +
                                    $"AVI:{log.AviPath}");
                                break;

                            case "DICOM_VIDEO":
                                Common.WriteSessionLog(
                                    $"[DICOM VIDEO RECOVER] User:{Common.CurrentUserId} " +
                                    $"Patient:{log.PatientName}({log.PatientCode}) " +
                                    $"AVI:{log.AviPath} DCM:{log.DicomPath}");
                                break;
                                // ✅ PATIENT 세션 로그는 위에서 이미 처리했으니 제거
                        }

                        // ✅ PATIENT 는 LoadLogs() 로 이미 전체 새로고침했으니 스킵
                        if (log.FileType != "PATIENT")
                            await UpdateItemInPlace(log, isRecover: true);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);

                        foreach (var (from, to) in Enumerable.Reverse(renamedFiles))
                        {
                            try
                            {
                                if (File.Exists(to))
                                    File.Move(to, from);
                            }
                            catch (Exception rollbackEx)
                            {
                                await Common.WriteLog(rollbackEx);
                            }
                        }
                    }
                }

                await CustomMessageWindow.ShowAsync("복구가 완료되었습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task RestoreFile(string filePath, List<(string From, string To)> renamedFiles)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

                string dir = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                if (!fileName.StartsWith("Del_")) return;

                string restoredName = fileName.Substring(4);
                string restoredPath = Path.Combine(dir, restoredName);

                File.Move(filePath, restoredPath);
                renamedFiles.Add((filePath, restoredPath));
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                throw;
            }
        }

        private async Task RestoreIsfFile(string dcmPath, List<(string From, string To)> renamedFiles)
        {
            try
            {
                if (string.IsNullOrEmpty(dcmPath)) return;

                string dicomDir = Path.Combine(Common.executablePath, "DICOM");
                string isfDir = Path.Combine(Common.executablePath, "ISF");

                string fileName = Path.GetFileNameWithoutExtension(dcmPath);
                string cleanName = fileName.StartsWith("Del_") ? fileName.Substring(4) : fileName;
                string studyDir = Path.GetDirectoryName(Path.GetDirectoryName(dcmPath));
                string relative = studyDir.Substring(dicomDir.Length)
                                           .TrimStart(Path.DirectorySeparatorChar);

                string isfPath = Path.Combine(isfDir, relative, "Del_" + cleanName + ".isf");
                await RestoreFile(isfPath, renamedFiles);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                throw;
            }
        }

        #endregion

        #region 강제 삭제

        private async Task ExecuteForceDelete()
        {
            try
            {
                var targets = FilteredLogs?
                    .Where(x => x.IsChecked)
                    .OrderBy(x => x.FileType == "PATIENT" ? 1 : 0) // ✅ PATIENT 무조건 맨 마지막 처리
                    .ToList();

                if (targets == null || targets.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync("즉시 삭제할 항목을 선택해주세요.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // ★ OTP 검증
                var otpDialog = new ForceDeleteOTP();
                bool passed = await otpDialog.ShowAsync();
                if (!passed) return;

                await ResetViewer();

                var db = new DB_Manager();

                foreach (var log in targets)
                {
                    try
                    {
                        switch (log.FileType)
                        {
                            case "IMAGE":
                                await DeleteFileIfExists(log.ImagePath);
                                await DeleteIsfFile(log.ImagePath);
                                break;

                            case "NORMAL_VIDEO":
                                await DeleteFileIfExists(log.AviPath);
                                break;

                            case "DICOM_VIDEO":
                                await DeleteFileIfExists(log.AviPath);
                                await DeleteFileIfExists(log.DicomPath);
                                break;

                            // ✅ PATIENT 강제삭제
                            case "PATIENT":
                                // 1. DELETE_LOG + PATIENT 테이블 트랜잭션
                                //    PATIENT_CODE + PATIENT_NAME 100% 동일해야 삭제
                                if (!db.ForceDeletePatientWithLog(log.DeleteId, log.PatientCode, log.PatientName))
                                    break;

                                // 2. DICOM/VIDEO 폴더 삭제
                                //    환자명_환자코드 조합으로 폴더 찾아서 완전 삭제
                                string folderName = $"{log.PatientName}_{log.PatientCode}";
                                string dicomPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM", folderName);
                                string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VIDEO", folderName);

                                if (Directory.Exists(dicomPath))
                                    Directory.Delete(dicomPath, recursive: true);

                                if (Directory.Exists(videoPath))
                                    Directory.Delete(videoPath, recursive: true);

                                // 3. 전체 리스트 새로고침
                                await LoadLogs();
                                break;
                        }

                        // ✅ PATIENT 는 ForceDeletePatientWithLog 내부에서 이미 처리했으니 스킵
                        if (log.FileType != "PATIENT")
                            db.UpdateForceDeleted(log.DeleteId);

                        Common.WriteSessionLog(
                            $"[FORCE DELETE] User:{Common.CurrentUserId} " +
                            $"Patient:{log.PatientName}({log.PatientCode}) " +
                            $"Type:{log.FileType} DeleteId:{log.DeleteId}");

                        // ✅ PATIENT 는 LoadLogs() 로 이미 전체 새로고침했으니 스킵
                        if (log.FileType != "PATIENT")
                            await UpdateItemInPlace(log, isRecover: false);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }

                await CustomMessageWindow.ShowAsync("완전 삭제가 완료되었습니다.", CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task DeleteFileIfExists(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return;
                if (!File.Exists(filePath)) return;
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
            
        }

        private async Task DeleteIsfFile(string dcmPath)
        {
            try
            {
                if (string.IsNullOrEmpty(dcmPath)) return;

                string dicomDir = Path.Combine(Common.executablePath, "DICOM");
                string isfDir = Path.Combine(Common.executablePath, "ISF");

                string fileName = Path.GetFileNameWithoutExtension(dcmPath);
                string cleanName = fileName.StartsWith("Del_") ? fileName.Substring(4) : fileName;
                string studyDir = Path.GetDirectoryName(Path.GetDirectoryName(dcmPath));
                string relative = studyDir.Substring(dicomDir.Length)
                                           .TrimStart(Path.DirectorySeparatorChar);

                string isfPath = Path.Combine(isfDir, relative, "Del_" + cleanName + ".isf");
                await DeleteFileIfExists(isfPath);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}