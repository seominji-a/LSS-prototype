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
        private const int EXPIRE_HOURS = 72;   // 72시간 후 만료 (병원마다 직접 수정)
        private const int PREVIEW_DELAY = 1000; // 미리보기 딜레이 1초 (무분별한 클릭 방지)

        // ── DB 원본 데이터 ──
        private List<RecoveryModel> _allLogs = new List<RecoveryModel>();

        // ── 미리보기 딜레이용 취소 토큰 ──
        // 행을 빠르게 여러 번 클릭할 때 이전 작업 취소 → 렉 방지
        private CancellationTokenSource _previewCts;

        #region 바인딩 프로퍼티

        // ── ISF 이미지 크기 (스케일 변환용) ──
        public double PreviewImageWidth { get; private set; }
        public double PreviewImageHeight { get; private set; }

        // ── 검색 딜레이용 취소 토큰 ──
        private CancellationTokenSource _searchCts;

        // ── ISF 드로잉 데이터 ──
        // ISF 없으면 빈 StrokeCollection → InkCanvas 에 아무것도 안 그려짐
        private StrokeCollection _currentStrokes;
        public StrokeCollection CurrentStrokes
        {
            get => _currentStrokes;
            set { _currentStrokes = value; OnPropertyChanged(); }
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
            set { _filteredLogs = value; OnPropertyChanged(); }
        }

        // ── 선택된 행 ──
        // 행 클릭 = 미리보기 트리거 (체크박스와 독립)
        // 체크박스 = 복구/강제삭제 작업 대상 선택 (행 선택과 독립)
        private RecoveryModel _selectedLog;
        public RecoveryModel SelectedLog
        {
            get => _selectedLog;
            set
            {
                _selectedLog = value;
                OnPropertyChanged();

                // 이전 미리보기 작업 취소 후 새로 시작
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
            set { _selectedFilter = value; OnPropertyChanged(); ApplyFilter(); }
        }

        // ── 검색어 ──
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();

                // 즉시 호출 → 0.5초 딜레이 후 호출로 변경
                // 빠르게 타이핑 시 이전 작업 취소 → 마지막 입력 후 0.5초 뒤 실행
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                _ = ApplyFilterWithDelayAsync(_searchCts.Token);
            }
        }

        // ── 뷰어 상태 ──
        private bool _isViewerEmpty = true;
        public bool IsViewerEmpty
        {
            get => _isViewerEmpty;
            set { _isViewerEmpty = value; OnPropertyChanged(); }
        }

        // IMAGE 타입 선택 시 이미지 뷰어 표시
        private bool _isImageVisible;
        public bool IsImageVisible
        {
            get => _isImageVisible;
            set { _isImageVisible = value; OnPropertyChanged(); }
        }

        // VIDEO 타입 선택 시 MediaElement 표시
        private bool _isVideoVisible;
        public bool IsVideoVisible
        {
            get => _isVideoVisible;
            set { _isVideoVisible = value; OnPropertyChanged(); }
        }

        // ── 뷰어 데이터 ──
        private WriteableBitmap _previewImageSource;
        public WriteableBitmap PreviewImageSource
        {
            get => _previewImageSource;
            set { _previewImageSource = value; OnPropertyChanged(); }
        }

        // AVI 파일 경로 - xaml.cs 에서 PropertyChanged 감지 → MediaElement 재생
        private string _previewVideoPath;
        public string PreviewVideoPath
        {
            get => _previewVideoPath;
            set { _previewVideoPath = value; OnPropertyChanged(); }
        }

        // 뷰어 상단에 표시되는 파일 경로
        private string _selectedFilePath;
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set { _selectedFilePath = value; OnPropertyChanged(); }
        }

        // 뷰어 상단에 표시되는 파일 타입
        private string _selectedFileType;
        public string SelectedFileType
        {
            get => _selectedFileType;
            set { _selectedFileType = value; OnPropertyChanged(); }
        }

        // ISF 저장 당시 캔버스 크기 - Recovery.xaml.cs 스케일 변환 시 사용
        public double OriginalCanvasWidth { get; private set; }
        public double OriginalCanvasHeight { get; private set; }

        #endregion

        #region 커맨드

        public ICommand NavigateBackCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand RecoverCommand { get; }   // 체크된 항목 복구
        public ICommand ForceDeleteCommand { get; }   // 체크된 항목 완전 삭제

        #endregion

        #region 생성자

        public RecoveryViewModel()
        {
            NavigateBackCommand = new RelayCommand(_ =>
                MainPage.Instance.NavigateTo(new User()));

            ExitCommand = new RelayCommand(Common.ExcuteExit);
            RecoverCommand = new RelayCommand(_ => ExecuteRecover());
            ForceDeleteCommand = new RelayCommand(_ => ExecuteForceDelete());

            // 화면 진입 시 DB 데이터 로드
            LoadLogs();
        }

        #endregion

        #region 데이터 로드

        // ═══════════════════════════════════════════
        //  LoadLogs()
        //  DB 에서 DELETE_LOG 전체 읽어오기
        //
        //  처리 순서:
        //  1. DELETED_AT 기준으로 만료 여부 / 남은 시간 계산
        //  2. 복구완료 / 강제삭제 항목은 RemainText 별도 표시
        //  3. 모든 항목 IsChecked = false 초기화
        //     → 복구/강제삭제 완료 후 LoadLogs() 재호출 시 체크 자동 해제
        // ═══════════════════════════════════════════
        private void LoadLogs()
        {
            try
            {
                var db = new DB_Manager();
                var logs = db.GetDeleteLogs();

                foreach (var log in logs)
                {
                    // 만료까지 남은 시간 계산
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

                    // 복구완료 / 강제삭제 항목은 RemainText 덮어씀
                    if (log.IsRecovered == "Y") log.RemainText = "복구처리";
                    if (log.IsForceDeleted == "Y") log.RemainText = "강제삭제";

                    // 체크박스 전체 초기화
                    log.IsChecked = false;
                }

                _allLogs = logs;
                ApplyFilter();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        #endregion

        #region 필터 / 검색

        // ═══════════════════════════════════════════
        //  ApplyFilterWithDelayAsync()
        //  검색어 입력 후 0.5초 딜레이 후 필터 적용
        //  타이핑 중 취소 → 마지막 입력 후 0.5초 뒤 실행
        //  데이터가 많아져도 불필요한 검색 반복 방지
        // ═══════════════════════════════════════════
        private async Task ApplyFilterWithDelayAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(500, ct);
                if (ct.IsCancellationRequested) return;
                ApplyFilter();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Common.WriteLog(ex); }

        }

        // ═══════════════════════════════════════════
        //  ApplyFilter()
        //  FILE_TYPE 필터 + 검색어 적용
        //
        //  검색 조건:
        //  - 공백 무시 (parkhanyong → park hanyong 검색 가능)
        //  - 대소문자 무시
        //  - 환자코드 검색 가능
        // ═══════════════════════════════════════════
        private void ApplyFilter()
        {
            try
            {
                var result = _allLogs.AsEnumerable();

                // FILE_TYPE 필터
                if (SelectedFilter != "ALL")
                    result = result.Where(x => x.FileType == SelectedFilter);

                // 검색어 필터
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    string kw = SearchText.Trim();
                    string kwNoSpace = kw.Replace(" ", "");

                    result = result.Where(x =>
                    {
                        string displayNoSpace = x.DisplayName.Replace(" ", "");
                        return
                            displayNoSpace.IndexOf(kwNoSpace, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            x.DisplayName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            x.PatientCode.ToString().Contains(kw);
                    });
                }

                FilteredLogs = new ObservableCollection<RecoveryModel>(result);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        #endregion

        #region 미리보기

        // ═══════════════════════════════════════════
        //  LoadPreviewAsync()
        //  행 클릭 시 미리보기 로드 (1초 딜레이)
        //
        //  미리보기 불가 조건:
        //  - 만료된 항목 (파일이 실제로 없음)
        //  - 복구완료 항목
        //  - 강제삭제 항목 (파일이 완전 삭제됨)
        //
        //  1초 딜레이 이유:
        //  빠르게 여러 행을 클릭할 때 이전 작업을 취소하여 렉 방지
        // ═══════════════════════════════════════════
        private async Task LoadPreviewAsync(CancellationToken ct)
        {
            try
            {
                ResetViewer();

                if (_selectedLog == null) return;
                if (_selectedLog.IsExpired) return;
                if (_selectedLog.IsRecovered == "Y") return;
                if (_selectedLog.IsForceDeleted == "Y") return;

                await Task.Delay(PREVIEW_DELAY, ct);
                if (ct.IsCancellationRequested) return;

                switch (_selectedLog.FileType)
                {
                    case "IMAGE":
                        await LoadDicomPreviewAsync(_selectedLog.ImagePath, ct);
                        break;
                    case "DICOM_VIDEO":
                    case "NORMAL_VIDEO":
                        LoadVideoPreview(_selectedLog.AviPath);
                        break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ResetViewer()
        //  행 선택이 바뀔 때마다 기존 미리보기 초기화
        // ═══════════════════════════════════════════
        private void ResetViewer()
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
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  LoadDicomPreviewAsync()
        //  DCM 파일 → WriteableBitmap 렌더링
        //
        //  fo-dicom DicomImage.RenderImage() 로 픽셀 추출
        //  렌더링은 무거우므로 백그라운드 스레드에서 처리
        //  WriteableBitmap 생성은 반드시 UI 스레드(Dispatcher)에서
        //  ISF 파일 있으면 CurrentStrokes 에 로드 → InkCanvas 오버레이
        // ═══════════════════════════════════════════
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

                LoadIsfStrokes(dcmPath);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  LoadVideoPreview()
        //  AVI 경로 → PreviewVideoPath 에 세팅
        //  xaml.cs 의 PropertyChanged 감지
        //  → MediaElement.Source 변경 + 자동 재생
        // ═══════════════════════════════════════════
        private void LoadVideoPreview(string aviPath)
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
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  LoadIsfStrokes()
        //  DCM 경로 기준으로 ISF 파일 경로 계산 후 로드
        //  ISF 파일 없으면 빈 StrokeCollection (드로잉 안 한 경우)
        //  에러 발생 시 빈 StrokeCollection 으로 fallback
        //  → 절대 이미지 미리보기 자체를 막으면 안 됨
        // ═══════════════════════════════════════════
        private void LoadIsfStrokes(string dcmPath)
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

                // ISF 안에 저장된 캔버스 크기 읽기 (스케일 변환용)
                var guidWidth = new Guid("A1B2C3D4-0001-0002-0003-000000000001");
                var guidHeight = new Guid("A1B2C3D4-0001-0002-0003-000000000002");

                OriginalCanvasWidth = strokes.ContainsPropertyData(guidWidth)
                    ? double.Parse(strokes.GetPropertyData(guidWidth).ToString()) : 1465;
                OriginalCanvasHeight = strokes.ContainsPropertyData(guidHeight)
                    ? double.Parse(strokes.GetPropertyData(guidHeight).ToString()) : 1060;

                CurrentStrokes = strokes;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                CurrentStrokes = new StrokeCollection();
            }
        }

        #endregion

        #region 복구 실행

        // ═══════════════════════════════════════════
        //  ExecuteRecover()
        //  체크된 항목들을 복구 (Del_ 제거 → 원래 파일명 복원)
        //
        //  IsCheckable 프로퍼티로 만료/복구완료/강제삭제 항목은
        //  체크박스 자체가 비활성화되어 있으므로
        //  별도 유효성 검사 없이 체크된 항목만 처리
        //
        //  각 항목은 독립적으로 처리
        //  → 한 항목 실패 시 그 항목만 롤백, 나머지는 계속 진행
        // ═══════════════════════════════════════════
        private void ExecuteRecover()
        {
            try
            {
                // 체크된 항목 수집
                var targets = FilteredLogs?
                    .Where(x => x.IsChecked)
                    .ToList();

                if (targets == null || targets.Count == 0)
                {
                    CustomMessageWindow.Show(
                        "복구할 항목을 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // 확인 팝업
                var confirm = CustomMessageWindow.Show(
                    $"{targets.Count}개 항목을 복구하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Info);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();

                foreach (var log in targets)
                {
                    // 항목별 독립적인 renamedFiles
                    // 실패 시 이 항목만 롤백
                    var renamedFiles = new List<(string From, string To)>();

                    try
                    {
                        switch (log.FileType)
                        {
                            case "IMAGE":
                                RestoreFile(log.ImagePath, renamedFiles);
                                RestoreIsfFile(log.ImagePath, renamedFiles);
                                break;
                            case "DICOM_VIDEO":
                                RestoreFile(log.AviPath, renamedFiles);
                                RestoreFile(log.DicomPath, renamedFiles);
                                break;
                            case "NORMAL_VIDEO":
                                RestoreFile(log.AviPath, renamedFiles);
                                break;
                        }

                        // 파일 복구 성공 → DB UPDATE
                        db.UpdateRecovered(log.DeleteId);

                        // 세션 로그
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
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.WriteLog(ex);

                        // 이 항목만 롤백 (성공했던 파일 → 다시 Del_ 로 되돌림)
                        foreach (var (from, to) in Enumerable.Reverse(renamedFiles))
                        {
                            try { if (File.Exists(to)) File.Move(to, from); }
                            catch (Exception rollbackEx) { Common.WriteLog(rollbackEx); }
                        }
                    }
                }

                CustomMessageWindow.Show(
                    "복구가 완료되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Info);

                // LoadLogs() 에서 IsChecked = false 초기화 + 목록 갱신
                LoadLogs();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  RestoreFile()
        //  파일명에서 Del_ 제거 (단일 파일 복구)
        //  Del_박한용_001.dcm → 박한용_001.dcm
        //
        //  renamedFiles 에 기록하는 이유:
        //  복구 도중 실패 시 성공한 파일들을 다시 Del_ 로 되돌리기 위해
        //
        //  파일이 없거나 Del_ 로 시작하지 않으면 그냥 패스
        //  → ISF 처럼 없을 수도 있는 파일도 안전하게 처리
        // ═══════════════════════════════════════════
        private void RestoreFile(string filePath, List<(string From, string To)> renamedFiles)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

                string dir = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                if (!fileName.StartsWith("Del_")) return;

                string restoredName = fileName.Substring(4); // "Del_" 4글자 제거
                string restoredPath = Path.Combine(dir, restoredName);

                File.Move(filePath, restoredPath);
                renamedFiles.Add((filePath, restoredPath));
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                throw; // ExecuteRecover 의 catch 에서 롤백 처리
            }
        }

        // ═══════════════════════════════════════════
        //  RestoreIsfFile()
        //  DCM 경로 기준으로 ISF 경로 계산 후 복구
        //  ISF 가 없으면 드로잉 안 한 것 → 그냥 패스
        // ═══════════════════════════════════════════
        private void RestoreIsfFile(string dcmPath, List<(string From, string To)> renamedFiles)
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
                RestoreFile(isfPath, renamedFiles);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                throw;
            }
        }

        #endregion

        #region 강제 삭제

        // ═══════════════════════════════════════════
        //  ExecuteForceDelete()
        //  체크된 항목들을 완전 삭제 (File.Delete - 복구 불가)
        //
        //  IsCheckable 프로퍼티로 만료/복구완료/강제삭제 항목은
        //  체크박스 자체가 비활성화되어 있으므로
        //  별도 유효성 검사 없이 체크된 항목만 처리
        //
        //  파일 삭제 실패 시 로그만 남기고 다음 항목으로 진행
        //  → 파일이 이미 없는 경우에도 DB 는 강제삭제 처리
        // ═══════════════════════════════════════════
        private void ExecuteForceDelete()
        {
            try
            {
                // 체크된 항목 수집
                var targets = FilteredLogs?
                    .Where(x => x.IsChecked)
                    .ToList();

                if (targets == null || targets.Count == 0)
                {
                    CustomMessageWindow.Show(
                        "즉시 삭제할 항목을 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // 확인 팝업 (강조 경고)
                var confirm = CustomMessageWindow.Show(
                    $"선택한 {targets.Count}개 항목을 완전 삭제하시겠습니까?\n\n삭제된 파일은 복구할 수 없습니다.",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Warning);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();

                foreach (var log in targets)
                {
                    try
                    {
                        // FileType 기준으로 관련 파일 전부 완전 삭제
                        switch (log.FileType)
                        {
                            case "IMAGE":
                                DeleteFileIfExists(log.ImagePath); // DCM 삭제
                                DeleteIsfFile(log.ImagePath);      // ISF 삭제
                                break;
                            case "NORMAL_VIDEO":
                                DeleteFileIfExists(log.AviPath);   // AVI 삭제
                                break;
                            case "DICOM_VIDEO":
                                DeleteFileIfExists(log.AviPath);   // AVI 삭제
                                DeleteFileIfExists(log.DicomPath); // DCM 삭제
                                break;
                        }

                        // 파일 삭제 완료 → DB UPDATE (IS_FORCE_DELETED = Y)
                        db.UpdateForceDeleted(log.DeleteId);

                        // 세션 로그
                        Common.WriteSessionLog(
                            $"[FORCE DELETE] User:{Common.CurrentUserId} " +
                            $"Patient:{log.PatientName}({log.PatientCode}) " +
                            $"Type:{log.FileType} DeleteId:{log.DeleteId}");
                    }
                    catch (Exception ex)
                    {
                        // 실패해도 로그만 남기고 다음 항목 계속 진행
                        Common.WriteLog(ex);
                    }
                }

                CustomMessageWindow.Show(
                    "완전 삭제가 완료되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Info);

                // LoadLogs() 에서 IsChecked = false 초기화 + 목록 갱신
                LoadLogs();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  DeleteFileIfExists()
        //  파일 존재 시 완전 삭제 (휴지통 아님)
        //  경로가 비어있거나 파일이 없으면 그냥 패스
        // ═══════════════════════════════════════════
        private void DeleteFileIfExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            if (!File.Exists(filePath)) return;
            File.Delete(filePath);
        }

        // ═══════════════════════════════════════════
        //  DeleteIsfFile()
        //  DCM 경로 기준으로 ISF 경로 계산 후 삭제
        //  ISF 파일이 없으면 그냥 패스 (드로잉 안 한 경우)
        // ═══════════════════════════════════════════
        private void DeleteIsfFile(string dcmPath)
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
                DeleteFileIfExists(isfPath);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
