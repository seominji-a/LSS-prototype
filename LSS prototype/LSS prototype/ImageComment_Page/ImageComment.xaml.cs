using LSS_prototype.Patient_Page;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace LSS_prototype.ImageComment_Page
{
    public partial class ImageComment : UserControl
    {
        // ═══════════════════════════════════════════
        //  코드비하인드 역할
        //  InkCanvas(그림판) UI 조작만 담당
        //  데이터/로직/팝업은 전부 ViewModel
        // ═══════════════════════════════════════════

        // 펜 속성 (UI 전용 - ViewModel 불필요)
        private Color _penColor = Color.FromRgb(0xFF, 0x44, 0x44);
        private double _penThickness = 4.0;

        // 실제로 새 드로잉 작업을 했는지 여부
        // ISF 로드 직후 false, 획 추가/삭제 시 true
        private bool _isDirty = false;

        // 버튼 색상 (토글 상태 표현용)
        private static readonly SolidColorBrush BrushAccent = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        private static readonly SolidColorBrush BrushBtnDark = new SolidColorBrush(Color.FromRgb(0x2A, 0x3F, 0x55));

        // Back 시 Scan 화면에 환자 정보를 다시 전달하기 위해 보관
        private readonly PatientModel _patient;

        // 현재 세션 번호 - 이 세션의 이미지만 로드하기 위해 보관
        private readonly string _seriesNumber;

        private readonly int _instanceIndex;  // 마지막으로 촬영한 번호 ( 코멘트에서 다시 스캔으로 돌아왔을때 이어서 촬영하기 위해 )

        private ImageCommentViewModel VM => DataContext as ImageCommentViewModel;

        // ═══════════════════════════════════════════
        //  생성자
        //  ScanViewModel.OpenImageComment() 에서
        //  selectedPatient 와 seriesNumber 를 함께 받음
        //  → 현재 세션 이미지만 로드하기 위해
        // ═══════════════════════════════════════════
        public ImageComment(PatientModel selectedPatient, string seriesNumber, int instanceIndex)
        {
            _patient = selectedPatient;
            _seriesNumber = seriesNumber;
            _instanceIndex = instanceIndex;  

            InitializeComponent();
            DataContext = new ImageCommentViewModel();

            // ViewModel 프로퍼티 변경 감지
            // CurrentImage  변경 → CapturedImage.Source 갱신
            // CurrentStrokes 변경 → DrawingCanvas.Strokes 갱신
            VM.PropertyChanged += OnViewModelPropertyChanged;

            Loaded += (s, e) => OnLoaded();
        }

        // ═══════════════════════════════════════════
        //  Loaded
        //  ViewModel.Initialize() 로 현재 세션 DCM 목록 수집 및 첫 페이지 로드
        //  실패 시 팝업은 ViewModel 에서 처리
        // ═══════════════════════════════════════════
        private void OnLoaded()
        {
            try
            {
                // patient + seriesNumber 둘 다 넘겨줌
                // → 현재 세션 폴더만 탐색
                bool success = VM.Initialize(_patient, _seriesNumber);
                if (!success) return;

                DrawingCanvas.EditingMode = InkCanvasEditingMode.None;
                ApplyDrawingAttributes();
                DrawingCanvas.PreviewMouseDown += OnCanvasPreviewMouseDown;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ViewModel 프로퍼티 변경 감지
        //  CurrentImage   → CapturedImage.Source 갱신
        //  CurrentStrokes → DrawingCanvas.Strokes 갱신
        // ═══════════════════════════════════════════
        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(VM.CurrentImage))
                    CapturedImage.Source = VM.CurrentImage;

                if (e.PropertyName == nameof(VM.CurrentStrokes))
                {
                    // 기존 구독 해제 후 새 StrokeCollection 연결
                    DrawingCanvas.Strokes.StrokesChanged -= OnStrokesChanged;
                    DrawingCanvas.Strokes = VM.CurrentStrokes ?? new StrokeCollection();
                    _isDirty = false;
                    DrawingCanvas.Strokes.StrokesChanged += OnStrokesChanged;
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  획 추가/삭제 감지 → _isDirty ON
        //  페이지 이동 시 저장 여부 확인에 사용
        // ═══════════════════════════════════════════
        private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            _isDirty = true;
        }

        // ═══════════════════════════════════════════
        //  좌/우 20% 터치 → 페이지 이동
        //  PEN/ERASE 모드일 때는 이동 안함 (그림 그리는 중)
        //  _isDirty 시 ViewModel 에 저장 여부 확인 요청
        // ═══════════════════════════════════════════
        private void OnCanvasPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (DrawingCanvas.EditingMode != InkCanvasEditingMode.None) return;

                double ratio = e.GetPosition(DrawingCanvas).X / DrawingCanvas.ActualWidth;
                if (DrawingCanvas.ActualWidth <= 0) return;

                bool goNext = ratio >= 0.80;
                bool goPrev = ratio <= 0.20;
                if (!goNext && !goPrev) return;

                e.Handled = true;

                // 새 드로잉이 있으면 저장 여부 확인
                if (_isDirty)
                {
                    bool save = VM.ConfirmSaveDrawing();
                    if (save) VM.SaveIsf(DrawingCanvas.Strokes);
                }

                // 페이지 이동 성공 시 펜 모드 해제
                bool moved = VM.NavigatePage(goNext);
                if (moved) SetDrawingMode(false);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  DrawingAttributes 적용
        //  색상/굵기 변경 시마다 호출
        // ═══════════════════════════════════════════
        private void ApplyDrawingAttributes()
        {
            DrawingCanvas.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = _penColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true,
                IgnorePressure = true,
                StylusTip = StylusTip.Ellipse
            };
        }

        // ═══════════════════════════════════════════
        //  드로잉 모드 전환
        //  on=true  → Ink (그리기)
        //  on=false → None (터치 이동 모드)
        // ═══════════════════════════════════════════
        private void SetDrawingMode(bool on)
        {
            if (on)
            {
                DrawingCanvas.EditingMode = InkCanvasEditingMode.Ink;
                BtnPen.Background = BrushAccent;
                BtnErase.Background = BrushBtnDark;
            }
            else
            {
                DrawingCanvas.EditingMode = InkCanvasEditingMode.None;
                BtnPen.Background = BrushBtnDark;
                BtnErase.Background = BrushBtnDark;
            }
        }

        // ═══════════════════════════════════════════
        //  PEN 토글
        //  현재 Ink 모드면 OFF, 아니면 ON
        // ═══════════════════════════════════════════
        private void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            try { SetDrawingMode(DrawingCanvas.EditingMode != InkCanvasEditingMode.Ink); }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ERASE 토글
        //  획 단위 지우기 모드 (EraseByStroke)
        // ═══════════════════════════════════════════
        private void BtnErase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DrawingCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
                {
                    SetDrawingMode(false);
                }
                else
                {
                    DrawingCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    BtnPen.Background = BrushBtnDark;
                    BtnErase.Background = BrushAccent;
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  CLEAR - 전체 획 삭제
        // ═══════════════════════════════════════════
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            try { DrawingCanvas.Strokes.Clear(); }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  색상 버튼 클릭 → 펜 색상 변경 + 자동 PEN ON
        //  Button.Tag 에 색상 HEX 값 저장
        // ═══════════════════════════════════════════
        private void ColorBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string hex)
                {
                    _penColor = (Color)ColorConverter.ConvertFromString(hex);
                    ApplyDrawingAttributes();
                    SetDrawingMode(true);
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  굵기 슬라이더
        //  숫자 박스(TxtThickness) 동기화
        // ═══════════════════════════════════════════
        private void SliderThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                _penThickness = e.NewValue;
                if (TxtThickness == null) return;
                TxtThickness.Text = ((int)_penThickness).ToString();
                ApplyDrawingAttributes();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  RESET
        //  InkCanvas 초기화 + ViewModel 입력값 초기화
        // ═══════════════════════════════════════════
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DrawingCanvas.Strokes.Clear();
                SetDrawingMode(false);
                VM.Reset();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  BACK → Scan 화면으로 복귀
        //  _patient 를 그대로 넘겨서 선택된 환자 유지
        // ═══════════════════════════════════════════
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            try { MainPage.Instance.NavigateTo(new Scan_Page.Scan(_patient, _seriesNumber, _instanceIndex)); }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
    }
}