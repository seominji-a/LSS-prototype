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

namespace LSS_prototype.ImageComment_Page
{
    public class ImageCommentViewModel : INotifyPropertyChanged
    {
        // ═══════════════════════════════════════════
        //  경로
        //  DICOM 과 ISF 는 동일한 폴더 구조로 관리
        //
        //  DICOM/
        //  └── 박한용_0225/
        //      └── 12340001/
        //          ├── 박한용_0225_12340001_1.dcm
        //          └── 박한용_0225_12340001_2.dcm
        //
        //  ISF/
        //  └── 박한용_0225/
        //      └── 12340001/
        //          ├── 박한용_0225_12340001_1.isf
        //          └── 박한용_0225_12340001_2.isf
        // ═══════════════════════════════════════════
        private string DicomDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
        private string IsfDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ISF");

        // DCM 경로 → ISF 경로 변환
        // DICOM/박한용_0225/12340001/파일.dcm
        //   → ISF/박한용_0225/12340001/파일.isf
        private string GetIsfPath(string dcmPath)
        {
            string relative = dcmPath.Substring(DicomDir.Length).TrimStart(Path.DirectorySeparatorChar);
            string isfRelative = Path.ChangeExtension(relative, ".isf");
            return Path.Combine(IsfDir, isfRelative);
        }

        // ═══════════════════════════════════════════
        //  DCM 파일 목록 & 현재 인덱스
        // ═══════════════════════════════════════════
        private List<string> _dcmFiles = new List<string>();
        private int _currentIndex = 0;

        // ═══════════════════════════════════════════
        //  바인딩 프로퍼티
        // ═══════════════════════════════════════════

        // 현재 표시 중인 이미지 (코드비하인드 CapturedImage.Source 에 바인딩)
        private WriteableBitmap _currentImage;
        public WriteableBitmap CurrentImage
        {
            get => _currentImage;
            set { _currentImage = value; OnPropertyChanged(); }
        }

        // 현재 페이지에 해당하는 ISF StrokeCollection
        // 코드비하인드에서 DrawingCanvas.Strokes 에 할당
        private StrokeCollection _currentStrokes;
        public StrokeCollection CurrentStrokes
        {
            get => _currentStrokes;
            set { _currentStrokes = value; OnPropertyChanged(); }
        }

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

        // Comment 텍스트박스
        private string _commentText;
        public string CommentText
        {
            get => _commentText;
            set { _commentText = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════
        //  커맨드
        // ═══════════════════════════════════════════
        public ICommand ImageDeleteCommand { get; }

        // ═══════════════════════════════════════════
        //  생성자
        // ═══════════════════════════════════════════
        public ImageCommentViewModel()
        {
            ImageDeleteCommand = new RelayCommand(_ => ExecuteImageDelete());
        }

        // ═══════════════════════════════════════════
        //  초기화 - 환자 DCM 파일 목록 수집
        //  OnLoaded 에서 호출
        //  반환값: 파일이 있으면 true, 없으면 false
        // ═══════════════════════════════════════════
        // ═══════════════════════════════════════════
        //  초기화 - 현재 세션 DCM 파일 목록 수집
        //  seriesNumber: ScanViewModel._currentSeriesNumber
        //  → 이 세션 폴더만 탐색 (이전 세션 이미지 제외)
        //  반환값: 파일이 있으면 true, 없으면 false
        // ═══════════════════════════════════════════
        public bool Initialize(PatientModel patient, string seriesNumber)
        {
            try
            {
                // 환자 폴더: DICOM/박한용_0225/세션번호/
                string patientFolder = $"{patient.PatientName}_{patient.PatientCode}";
                string seriesDir = Path.Combine(DicomDir, patientFolder, seriesNumber);

                // ISF 도 동일한 구조로 폴더 미리 생성
                string isfSeriesDir = Path.Combine(IsfDir, patientFolder, seriesNumber);
                Directory.CreateDirectory(isfSeriesDir);

                if (!Directory.Exists(seriesDir))
                {
                    CustomMessageWindow.Show(
                        "해당 세션의 DICOM 폴더가 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                // 현재 세션 폴더에서만 DCM 수집 (이전 세션 제외)
                // DICOM/박한용_0225/12340001/*.dcm
                _dcmFiles = Directory.EnumerateFiles(seriesDir, "*.dcm")
                    .OrderBy(f => f)
                    .ToList();

                if (_dcmFiles.Count == 0)
                {
                    CustomMessageWindow.Show(
                        "해당 환자의 DICOM 파일이 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                _currentIndex = 0;
                LoadPage(_currentIndex);
                return true;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        // ═══════════════════════════════════════════
        //  페이지 로드 - DCM 이미지 + ISF 드로잉
        //  CurrentImage, CurrentStrokes 프로퍼티 갱신
        //  코드비하인드가 프로퍼티 변경을 감지해서 UI 반영
        // ═══════════════════════════════════════════
        private void LoadPage(int index)
        {
            string dcmPath = _dcmFiles[index];
            // DCM 경로와 동일한 폴더 구조로 ISF 경로 계산
            // DICOM/박한용_0225/12340001/파일.dcm → ISF/박한용_0225/12340001/파일.isf
            string isfPath = GetIsfPath(dcmPath);

            // DCM → WriteableBitmap 변환
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
            if (File.Exists(isfPath))
            {
                using (var fs = File.OpenRead(isfPath))
                    CurrentStrokes = new StrokeCollection(fs);
            }
            else
            {
                CurrentStrokes = new StrokeCollection();
            }
        }

        // ═══════════════════════════════════════════
        //  ISF 저장
        //  코드비하인드에서 현재 StrokeCollection 을 넘겨줌
        //  드로잉 없으면 ISF 파일 삭제 (깔끔하게 유지)
        // ═══════════════════════════════════════════
        public void SaveIsf(StrokeCollection strokes)
        {
            try
            {
                string dcmPath = _dcmFiles[_currentIndex];
                // DCM 과 동일한 폴더 구조로 ISF 경로 계산
                string isfPath = GetIsfPath(dcmPath);

                if (strokes == null || strokes.Count == 0)
                {
                    if (File.Exists(isfPath)) File.Delete(isfPath);
                    return;
                }

                // ISF 저장 전 폴더 생성 (세션 폴더가 없을 수 있음)
                Directory.CreateDirectory(Path.GetDirectoryName(isfPath));

                using (var fs = File.Create(isfPath))
                    strokes.Save(fs);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  드로잉 저장 여부 확인 팝업
        //  Yes → true (코드비하인드에서 SaveIsf 호출)
        //  No  → false (저장 없이 페이지 이동)
        // ═══════════════════════════════════════════
        public bool ConfirmSaveDrawing()
        {
            try
            {
                var result = CustomMessageWindow.Show(
                    "드로잉을 저장하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Info);

                return result == CustomMessageWindow.MessageBoxResult.Yes;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        // ═══════════════════════════════════════════
        //  페이지 이동
        //  goNext: true = 다음, false = 이전
        //  반환값: 이동 성공 여부
        //  코드비하인드에서 _isDirty 확인 후 호출
        // ═══════════════════════════════════════════
        public bool NavigatePage(bool goNext)
        {
            try
            {
                int targetIndex = goNext ? _currentIndex + 1 : _currentIndex - 1;

                if (targetIndex < 0 || targetIndex >= _dcmFiles.Count)
                {
                    CustomMessageWindow.Show(
                        goNext ? "마지막 이미지입니다." : "첫 번째 이미지입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    return false;
                }

                _currentIndex = targetIndex;
                LoadPage(_currentIndex);
                return true;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        // ═══════════════════════════════════════════
        //  이미지 삭제
        //  확인 팝업 → Yes 시 삭제 진행
        //  반환값: 삭제 확정 여부 (코드비하인드에서 View 정리)
        // ═══════════════════════════════════════════
        private bool ExecuteImageDelete()
        {
            try
            {
                var result = CustomMessageWindow.Show(
                    "이미지를 삭제하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    icon: CustomMessageWindow.MessageIconType.Warning);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return false;

                // TODO: DB 레코드 삭제
                // TODO: DICOM 파일 삭제
                // TODO: ISF 파일 삭제

                return true;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        // ═══════════════════════════════════════════
        //  RESET - 우측 패널 입력값 초기화
        // ═══════════════════════════════════════════
        public void Reset()
        {
            SelectedPosition = null;
            SelectedAnatomical = null;
            CommentText = string.Empty;
        }

        // ═══════════════════════════════════════════
        //  INotifyPropertyChanged 구현
        //  프로젝트 전체 ViewModel 공통 패턴
        // ═══════════════════════════════════════════
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}