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
        //  └── 박한용_2634/
        //      └── 20250313/
        //          └── 202503130001/
        //              └── Image/
        //                  ├── 박한용_2634_202503130001_1.dcm
        //                  └── 박한용_2634_202503130001_2.dcm
        //
        //  ISF/( only image comment 이므로 별도의 image video 파일없이 시리즈 번호 밑에 바로 관리 ) 
        //  └── 박한용_2634/
        //      └── 20250313/
        //          └── 202503130001/
        //                          ├── 박한용_2634_202503130001_1.isf
        //                          └── 박한용_2634_202503130001_2.isf
        // ═══════════════════════════════════════════
        private string DicomDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
        private string IsfDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ISF");

        // ═══════════════════════════════════════════
        //  DCM 경로 → ISF 경로 변환
        // ═══════════════════════════════════════════
        private string GetIsfPath(string dcmPath)
        {
            // DCM 에서 파일명만 추출
            string fileName = Path.GetFileNameWithoutExtension(dcmPath);

            // 환자폴더/날짜폴더/studyId 경로 추출
            // DICOM/박한용_2634/20250313/202503130001/Image/ 에서
            // Image 폴더 위로 올라가야 함
            string studyDir = Path.GetDirectoryName(Path.GetDirectoryName(dcmPath));

            // DICOM 루트 기준 상대경로 계산
            string relative = studyDir.Substring(DicomDir.Length).TrimStart(Path.DirectorySeparatorChar);

            // ISF 경로 조합
            // ISF/박한용_2634/20250313/202503130001/파일.isf
            return Path.Combine(IsfDir, relative, fileName + ".isf");
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

        // 선택된 환자
        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set { _selectedPatient = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════
        //  커맨드
        // ═══════════════════════════════════════════
        public ICommand ImageDeleteCommand { get; }

        // ═══════════════════════════════════════════
        //  생성자
        // ═══════════════════════════════════════════
        public ImageCommentViewModel(PatientModel selectedPatient, string studyId = null)
        {
            SelectedPatient = selectedPatient;
            ImageDeleteCommand = new RelayCommand(_ => ExecuteImageDelete());
        }

        // ═══════════════════════════════════════════
        //  초기화 - 현재 StudyID 의 Image 폴더에서 DCM 목록 수집
        //  OnLoaded 에서 호출
        //  반환값: 파일이 있으면 true, 없으면 false
        // ═══════════════════════════════════════════
        public bool Initialize(PatientModel patient, string studyId)
        {
            try
            {
                if (patient == null || string.IsNullOrWhiteSpace(studyId))
                {
                    CustomMessageWindow.Show(
                        "환자 정보 또는 Study 정보가 올바르지 않습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                // 환자 폴더명 생성 (예: 박한용_2634)
                string patientFolder = $"{patient.PatientName}_{patient.PatientCode}";

                // StudyID 앞 8자리 = 날짜 폴더 (예: 20250313)
                string studyDateFolder = studyId.Substring(0, 8);

                // DCM 탐색 경로
                // DICOM/박한용_2634/20250313/202503130001/Image/
                string imageDir = Path.Combine(DicomDir, patientFolder, studyDateFolder, studyId, "Image");

                // ISF 폴더 미리 생성
                // ISF/박한용_2634/20250313/202503130001/Image/
                string isfDir = Path.Combine(IsfDir, patientFolder, studyDateFolder, studyId);
                Directory.CreateDirectory(isfDir);

                if (!Directory.Exists(imageDir))
                {
                    CustomMessageWindow.Show(
                        "해당 세션의 DICOM 폴더가 없습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return false;
                }

                // Image 폴더에서 DCM 수집
                // 파일명 마지막 숫자(인스턴스 번호) 기준으로 정렬
                // 예: 박한용_2634_202503130001_1.dcm → 1
                _dcmFiles = Directory.EnumerateFiles(imageDir, "*.dcm")
                    .OrderBy(f =>
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        string last = name.Split('_').Last();
                        return int.TryParse(last, out int n) ? n : int.MaxValue;
                    })
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

            // DCM 경로 → ISF 경로 자동 계산
            // DICOM/.../Image/파일.dcm → ISF/.../파일.isf
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

                // DCM 경로 기반으로 ISF 경로 계산
                string isfPath = GetIsfPath(dcmPath);

                if (strokes == null || strokes.Count == 0)
                {
                    // 드로잉 없으면 ISF 파일 삭제 (깔끔하게 유지)
                    if (File.Exists(isfPath)) File.Delete(isfPath);
                    return;
                }

                // ISF 저장 전 폴더 생성
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
        //  페이지 이동 가능 여부 확인 (실제 이동 X)
        //  이동 불가능한 방향에서 저장 팝업 안 뜨게 하기 위해 사용
        // ═══════════════════════════════════════════
        public bool CanNavigate(bool goNext)
        {
            int targetIndex = goNext ? _currentIndex + 1 : _currentIndex - 1;
            return targetIndex >= 0 && targetIndex < _dcmFiles.Count;
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