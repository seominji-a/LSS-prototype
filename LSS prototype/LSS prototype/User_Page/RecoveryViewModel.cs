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
        private const int EXPIRE_HOURS = 72;   // 72시간 후 만료 ( 병원마다 정해진 값을 직접 수정해줘야함.  0317 박한용)
        private const int PREVIEW_DELAY = 1000; // 미리보기 딜레이 1초 (무분별한 클릭 방지)

        // ── DB 원본 데이터 ──
        private List<RecoveryModel> _allLogs = new List<RecoveryModel>();

        // ── 미리보기 딜레이용 취소 토큰 ──
        // 행을 빠르게 여러 번 클릭할 때 이전 작업을 취소하여 렉 방지
        private CancellationTokenSource _previewCts;

        #region 바인딩 프로퍼티
        public double PreviewImageWidth { get; private set; }
        public double PreviewImageHeight { get; private set; }

        // ISF 드로잉 데이터 (ImageComment 와 동일한 방식)
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
        // 값이 바뀌면 1초 후 미리보기 로드 시작
        private RecoveryModel _selectedLog;
        public RecoveryModel SelectedLog
        {
            get => _selectedLog;
            set
            {
                _selectedLog = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRecover));

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
            set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        // ── 복구 버튼 활성화 조건 ──
        // 행 선택 + 만료 안됨 + 아직 복구 안됨
        public bool CanRecover =>
            _selectedLog != null &&
            !_selectedLog.IsExpired &&
            _selectedLog.IsRecovered == "N";

        // ── 뷰어 상태 ──
        // 선택 전 or 만료 항목 → 안내 텍스트 표시
        private bool _isViewerEmpty = true;
        public bool IsViewerEmpty
        {
            get => _isViewerEmpty;
            set { _isViewerEmpty = value; OnPropertyChanged(); }
        }

        // IMAGE 타입 선택 시 Image 컨트롤 표시
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

        // DCM 렌더링 결과 (IMAGE 타입)
        private WriteableBitmap _previewImageSource;
        public WriteableBitmap PreviewImageSource
        {
            get => _previewImageSource;
            set { _previewImageSource = value; OnPropertyChanged(); }
        }

        // AVI 파일 경로 (VIDEO 타입) - xaml.cs 에서 감지해서 자동 재생
        private string _previewVideoPath;
        public string PreviewVideoPath
        {
            get => _previewVideoPath;
            set { _previewVideoPath = value; OnPropertyChanged(); }
        }

        // 뷰어 하단 파일 경로 표시
        private string _selectedFilePath;
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set { _selectedFilePath = value; OnPropertyChanged(); }
        }

        // 뷰어 하단 파일 타입 표시
        private string _selectedFileType;
        public string SelectedFileType
        {
            get => _selectedFileType;
            set { _selectedFileType = value; OnPropertyChanged(); }
        }

        // - ISF 저장 당시 캔버스 크기
        // Recovery XAML단에서 에서 스케일 변환 시 사용
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

            ExitCommand = new RelayCommand(Common.ExcuteExit);

            RecoverCommand = new RelayCommand(_ => ExecuteRecover());
            ForceDeleteCommand = new RelayCommand(_ => ExecuteForceDelete());

            // 화면 진입 시 DB 데이터 로드
            LoadLogs();
        }

        #endregion

        #region 데이터 로드

        // ═══════════════════════════════════════════
        //  DB에서 DELETE_LOG 전체 읽어오기
        //  읽어온 후 RemainText / IsExpired 계산해서 각 항목에 채워줌
        // ═══════════════════════════════════════════
        private void LoadLogs()
        {
            try
            {
                var db = new DB_Manager();
                var logs = db.GetDeleteLogs();

                foreach (var log in logs)
                {
                    // DELETED_AT 기준으로 만료까지 남은 시간 계산
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

                    // 이미 복구된 항목은 별도 표시
                    if (log.IsRecovered == "Y")
                        log.RemainText = "복구처리";
                }

                _allLogs = logs;
                ApplyFilter();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        #endregion

        #region 필터 / 검색

        // ═══════════════════════════════════════════
        //  FILE_TYPE 필터 + 검색어 적용
        //  Patient.xaml 과 동일한 검색 조건
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
        //  행 선택 시 미리보기 로드 (1초 딜레이)
        //  1초 안에 다른 행 클릭하면 이전 작업 취소 → 렉 방지
        //  만료된 항목은 파일이 실제 삭제됐으므로 미리보기 불가
        // ═══════════════════════════════════════════
        private async Task LoadPreviewAsync(CancellationToken ct)
        {
            try
            {
                // 뷰어 초기화 (이전 미리보기 제거)
                ResetViewer();

                if (_selectedLog == null) return;

                // 만료된 항목 → 파일 없음 → 미리보기 불가
                if (_selectedLog.IsExpired) return;

                if (_selectedLog.IsRecovered == "Y") return; // 이미 복구된 항목도 미리보기 불가 

                // 1초 대기 (빠른 클릭 시 취소됨)
                await Task.Delay(PREVIEW_DELAY, ct);

                if (ct.IsCancellationRequested) return;

                // FileType 기준 분기
                switch (_selectedLog.FileType)
                {
                    case "IMAGE":
                        // DCM 파일을 열어서 WriteableBitmap 으로 렌더링
                        await LoadDicomPreviewAsync(_selectedLog.ImagePath, ct);
                        break;

                    case "DICOM_VIDEO":
                    case "NORMAL_VIDEO":
                        // AVI 경로를 MediaElement 에 연결
                        LoadVideoPreview(_selectedLog.AviPath);
                        break;
                }
            }
            catch (OperationCanceledException) { } // 취소는 정상 흐름이므로 무시
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ISF 파일 로드 → CurrentStrokes 에 세팅
        //
        //  ISF 경로 계산 방식은 ImageCommentViewModel.GetIsfPath() 와 동일
        //  DCM 경로 기준으로 DICOM → ISF 폴더로 변환
        //
        //  파일 없음 → 빈 StrokeCollection (InkCanvas 에 아무것도 안 그려짐)
        //  파일 있음 → StrokeCollection 로드 → InkCanvas 오버레이
        //
        //  ★ 에러 발생 시 빈 StrokeCollection 으로 fallback
        //    절대 이미지 미리보기 자체를 막으면 안 됨
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

                // ★ 추가 - ISF 안에 저장된 캔버스 크기 읽기
                var guidWidth = new Guid("A1B2C3D4-0001-0002-0003-000000000001");
                var guidHeight = new Guid("A1B2C3D4-0001-0002-0003-000000000002");

                // 크기 정보가 있으면 가져오고 없으면 fallback 값 사용
                double originalWidth = strokes.ContainsPropertyData(guidWidth)
                    ? double.Parse(strokes.GetPropertyData(guidWidth).ToString()) : 1465;
                double originalHeight = strokes.ContainsPropertyData(guidHeight)
                    ? double.Parse(strokes.GetPropertyData(guidHeight).ToString()) : 1060;

                // ★ 추가 - 원본 크기 정보를 ViewModel 에 저장
                // Recovery.xaml.cs 에서 스케일 변환 시 사용
                OriginalCanvasWidth = originalWidth;
                OriginalCanvasHeight = originalHeight;

                CurrentStrokes = strokes;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                CurrentStrokes = new StrokeCollection();
            }
        }

        // ═══════════════════════════════════════════
        //  뷰어 초기화
        //  행 선택 바뀔 때마다 기존 미리보기 제거
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
        //  DCM 파일 → WriteableBitmap 렌더링
        //  ScanViewModel.LoadImageThumbnail() 과 동일한 방식 재사용
        //  fo-dicom 의 DicomImage.RenderImage() 로 픽셀 추출
        //  렌더링은 무거우므로 백그라운드 스레드에서 처리
        //  WriteableBitmap 생성은 반드시 UI 스레드(Dispatcher)에서
        //  ISF 있으면 CurrentStrokes 에 로드 → InkCanvas 오버레이
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

                // ★ bitmap 생성 완료 후 여기서 크기 저장
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
        //  AVI 경로 → MediaElement 에 연결
        //  경로를 PreviewVideoPath 에 세팅하면
        //  xaml.cs 의 PropertyChanged 감지 → MediaElement.Source 변경 + 자동 재생
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

        #endregion

        #region 복구 실행

        // ═══════════════════════════════════════════
        //  복구 실행
        //  파일 시스템 트랜잭션 패턴
        //  DB 트랜잭션과 달리 파일 시스템은 자동 롤백이 없음
        //  그래서 성공한 파일 목록을 직접 기억했다가
        //  중간에 실패하면 성공했던 파일들을 원래대로 되돌림
        //
        //  예) DICOM_VIDEO 복구 시
        //      ① AVI 복구 성공 → renamedFiles 에 기록
        //      ② DCM 복구 실패 → Exception 발생
        //      ③ catch 에서 renamedFiles 순회
        //      ④ 성공했던 AVI → 다시 Del_ 로 되돌림
        //      → 원래 상태 복원 완료
        // ═══════════════════════════════════════════
        private void ExecuteRecover()
        {
            // 성공적으로 이름을 바꾼 파일들을 순서대로 기록
            // (From = Del_ 붙은 원본 경로, To = Del_ 제거된 복구 경로)
            // 롤백 시 To → From 으로 되돌림
            var renamedFiles = new List<(string From, string To)>();

            try
            {
                // ── 행 선택 여부 확인 ──
                if (_selectedLog == null)
                {
                    CustomMessageWindow.Show("복구할 항목을 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // ── 만료 확인 ──
                // 만료된 항목은 파일이 실제로 삭제됐으므로 복구 불가
                if (_selectedLog.IsExpired)
                {
                    CustomMessageWindow.Show("이미 만료기한이 지나 삭제 처리가 되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // ── 이미 복구된 항목 확인 ──
                if (_selectedLog.IsRecovered == "Y")
                {
                    CustomMessageWindow.Show("이미 복구된 항목입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Info);
                    return;
                }

                // ── 복구 확인 팝업 ──
                var result = CustomMessageWindow.Show(
                    $"{_selectedLog.DisplayName} 환자의\n{_selectedLog.FileType} 파일을 복구하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Info);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                // ── FileType 기준으로 복구할 파일 분기 ──
                switch (_selectedLog.FileType)
                {
                    case "IMAGE":
                        // DCM 파일 복구
                        RestoreFile(_selectedLog.ImagePath, renamedFiles);
                        // ISF 파일 복구 (드로잉 없으면 파일 자체가 없을 수 있음 → 내부에서 처리)
                        RestoreIsfFile(_selectedLog.ImagePath, renamedFiles);
                        break;

                    case "DICOM_VIDEO":
                        // AVI + DCM 한 쌍 복구
                        // ★ 둘 중 하나라도 실패하면 catch 에서 둘 다 롤백
                        RestoreFile(_selectedLog.AviPath, renamedFiles);
                        RestoreFile(_selectedLog.DicomPath, renamedFiles);
                        break;

                    case "NORMAL_VIDEO":
                        // AVI 단독 복구
                        RestoreFile(_selectedLog.AviPath, renamedFiles);
                        break;
                }

                // ── 여기까지 왔으면 모든 파일 복구 성공 → DB 업데이트 ──
                var db = new DB_Manager();
                db.UpdateRecovered(_selectedLog.DeleteId);

                switch (_selectedLog.FileType)
                {
                    case "IMAGE":
                        Common.WriteSessionLog(
                            $"[IMAGE RECOVER] User:{Common.CurrentUserId} " +
                            $"Patient:{_selectedLog.PatientName}({_selectedLog.PatientCode}) " +
                            $"File:{_selectedLog.ImagePath}");
                        break;

                    case "NORMAL_VIDEO":
                        Common.WriteSessionLog(
                            $"[NORMAL VIDEO RECOVER] User:{Common.CurrentUserId} " +
                            $"Patient:{_selectedLog.PatientName}({_selectedLog.PatientCode}) " +
                            $"AVI:{_selectedLog.AviPath}");
                        break;

                    case "DICOM_VIDEO":
                        Common.WriteSessionLog(
                            $"[DICOM VIDEO RECOVER] User:{Common.CurrentUserId} " +
                            $"Patient:{_selectedLog.PatientName}({_selectedLog.PatientCode}) " +
                            $"AVI:{_selectedLog.AviPath} DCM:{_selectedLog.DicomPath}");
                        break;
                }


                CustomMessageWindow.Show("복구가 완료되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Info);

                // ── 목록 갱신 ──
                LoadLogs();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);

                // ── 롤백 ──
                // 파일 복구 도중 실패했으므로
                // 성공했던 파일들을 다시 Del_ 이름으로 되돌림
                // renamedFiles 를 역순으로 순회 (나중에 바꾼 것부터 되돌리는 게 안전)
                foreach (var (from, to) in Enumerable.Reverse(renamedFiles))
                {
                    try
                    {
                        // to = 복구된 경로 (Del_ 없음)
                        // from = 원본 경로 (Del_ 있음)
                        // 복구된 파일이 실제로 존재할 때만 되돌림
                        if (File.Exists(to))
                            File.Move(to, from);
                    }
                    catch (Exception rollbackEx)
                    {
                        // 롤백도 실패한 경우 → 로그만 남기고 계속 진행
                        // (다른 파일 롤백은 계속 시도해야 함)
                        Common.WriteLog(rollbackEx);
                    }
                }

                CustomMessageWindow.Show("복구 중 오류가 발생하여 취소되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 3,
                    CustomMessageWindow.MessageIconType.Warning);
            }
        }

        // ═══════════════════════════════════════════
        //  파일명에서 Del_ 제거 (단일 파일 복구)
        //
        //  동작 방식
        //  Del_박한용_001.dcm → 박한용_001.dcm 으로 파일명 변경
        //  File.Move = 실제 삭제/생성 아님, 이름만 바꿈
        //
        //  renamedFiles 에 기록하는 이유
        //  복구 도중 실패 시 롤백을 위해
        //  성공한 파일의 (원본경로, 복구경로) 쌍을 기억해둠
        //
        //  파일이 없거나 Del_ 로 시작하지 않으면 그냥 패스
        //  (ISF 처럼 없을 수도 있는 파일도 이 함수로 처리 가능)
        // ═══════════════════════════════════════════
        private void RestoreFile(string filePath, List<(string From, string To)> renamedFiles)
        {
            try
            {
                // 경로가 비어있거나 파일이 없으면 패스
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

                string dir = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                // Del_ 로 시작하지 않으면 이미 복구된 상태이므로 패스
                if (!fileName.StartsWith("Del_")) return;

                // "Del_" 4글자 제거 → 복구된 파일명
                // 예) Del_박한용_001.dcm → 박한용_001.dcm
                string restoredName = fileName.Substring(4);
                string restoredPath = Path.Combine(dir, restoredName);

                // 파일명 변경 (이름 바꾸기 = Move)
                File.Move(filePath, restoredPath);

                // ★ 성공한 경우에만 renamedFiles 에 기록
                // From = Del_ 붙은 원본, To = Del_ 제거된 복구본
                // 롤백 시 To → From 방향으로 되돌림
                renamedFiles.Add((filePath, restoredPath));
            }
            catch (Exception ex)
            {
                // 이 예외를 catch 하지 않고 위로 던짐
                // → ExecuteRecover 의 catch 에서 롤백 처리
                Common.WriteLog(ex);
                throw;
            }
        }

        private void RestoreIsfFile(string dcmPath, List<(string From, string To)> renamedFiles)
        {
            try
            {
                if (string.IsNullOrEmpty(dcmPath)) return;

                string dicomDir = Path.Combine(Common.executablePath, "DICOM");
                string isfDir = Path.Combine(Common.executablePath, "ISF");

                // DCM 파일명에서 Del_ 제거한 이름으로 ISF 경로 계산
                string fileName = Path.GetFileNameWithoutExtension(dcmPath);
                string cleanName = fileName.StartsWith("Del_") ? fileName.Substring(4) : fileName;
                string studyDir = Path.GetDirectoryName(Path.GetDirectoryName(dcmPath));
                string relative = studyDir.Substring(dicomDir.Length).TrimStart(Path.DirectorySeparatorChar);

                // Del_ 붙은 ISF 경로
                string isfPath = Path.Combine(isfDir, relative, "Del_" + cleanName + ".isf");

                // ISF 가 없으면 드로잉 안 한 것 → 패스
                // RestoreFile 내부에서도 File.Exists 체크하므로 그냥 넘겨도 됨
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
        private void ExecuteForceDelete()
        {
            return;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}