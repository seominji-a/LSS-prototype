using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
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
        //  팝업/로직은 전부 ViewModel
        // ═══════════════════════════════════════════

        private Color _penColor = Color.FromRgb(0xFF, 0x44, 0x44);
        private double _penThickness = 4.0;

        // ISF 드로잉 변경 감지 플래그
        // ISF 로드 직후 false, 획 추가/삭제 시 true
        private bool _isDirty = false;

        private static readonly SolidColorBrush BrushAccent = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        private static readonly SolidColorBrush BrushBtnDark = new SolidColorBrush(Color.FromRgb(0x2A, 0x3F, 0x55));

        private readonly PatientModel _patient;
        private readonly string _studyId;

        private ImageCommentViewModel VM => DataContext as ImageCommentViewModel;

        // ═══════════════════════════════════════════
        //  저장 필요 여부 통합 체크
        //  _isDirty (ISF 드로잉) OR VM.IsCommentDirty (코멘트)
        // ═══════════════════════════════════════════
        private bool HasUnsavedChanges() => _isDirty || VM.IsCommentDirty;

        // ═══════════════════════════════════════════
        //  생성자
        // ═══════════════════════════════════════════
        public ImageComment(PatientModel selectedPatient, string studyId)
        {
            _patient = selectedPatient;
            _studyId = studyId;

            InitializeComponent();
            DataContext = new ImageCommentViewModel(selectedPatient, studyId);

            VM.PropertyChanged += OnViewModelPropertyChanged;
            VM.RequestNavigateToScan += () => MainPage.Instance.NavigateTo(new Scan(_patient, _studyId));

            // SAVE 버튼 → DrawingCanvas 접근 필요하므로 코드비하인드에서 처리
            VM.RequestSave += () =>
            {
                VM.SaveComment(DrawingCanvas.Strokes, DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight);
                _isDirty = false;
                // VM.IsCommentDirty 는 SaveIsf 내부에서 리셋
            };

            Loaded += (s, e) => OnLoaded();
        }

        // ═══════════════════════════════════════════
        //  Loaded
        // ═══════════════════════════════════════════
        private async void OnLoaded()
        {
            try
            {
                bool success = await VM.Initialize(_patient, _studyId);
                if (!success) return;

                DrawingCanvas.EditingMode = InkCanvasEditingMode.None;
                ApplyDrawingAttributes();
                DrawingCanvas.PreviewMouseDown += OnCanvasPreviewMouseDown;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ViewModel 프로퍼티 변경 감지
        //  CurrentImage   → CapturedImage.Source 갱신
        //  CurrentStrokes → DrawingCanvas.Strokes 갱신 + _isDirty 리셋
        // ═══════════════════════════════════════════
        private async void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(VM.CurrentImage))
                    CapturedImage.Source = VM.CurrentImage;

                if (e.PropertyName == nameof(VM.CurrentStrokes))
                {
                    DrawingCanvas.Strokes.StrokesChanged -= OnStrokesChanged;
                    DrawingCanvas.Strokes = VM.CurrentStrokes ?? new StrokeCollection();
                    _isDirty = false;
                    DrawingCanvas.Strokes.StrokesChanged += OnStrokesChanged;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  획 추가/삭제 감지 → _isDirty ON
        // ═══════════════════════════════════════════
        private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            _isDirty = true;
        }

        // ═══════════════════════════════════════════
        //  좌/우 20% 터치 → 페이지 이동
        //
        //  1. CanNavigate() 로 이동 가능 여부 먼저 확인
        //     → 1장이거나 첫/마지막 장이면 팝업 없이 바로 리턴
        //  2. 이동 가능한 경우에만 HasUnsavedChanges() 확인
        //     → ISF 드로잉 또는 코멘트 변경이 있으면 ConfirmSaveAll() 팝업
        // ═══════════════════════════════════════════
        private async void OnCanvasPreviewMouseDown(object sender, MouseButtonEventArgs e)
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

                //   이동 불가면 팝업 없이 바로 리턴
                if (!VM.CanNavigate(goNext)) return;

                // 변경 사항 있으면 저장 여부 확인
                if (HasUnsavedChanges())
                {
                    bool save = await VM.ConfirmSaveAll();
                    if (save)
                    {
                        VM.SaveComment(DrawingCanvas.Strokes, DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight);
                        // VM.IsCommentDirty 는 SaveIsf 내부에서 리셋
                    }
                    _isDirty = false;
                }

                bool moved = await VM.NavigatePage(goNext);
                if (moved) SetDrawingMode(false);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  DrawingAttributes 적용
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
        // ═══════════════════════════════════════════
        private async void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            try { SetDrawingMode(DrawingCanvas.EditingMode != InkCanvasEditingMode.Ink); }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ERASE 토글
        // ═══════════════════════════════════════════
        private async void BtnErase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DrawingCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
                    SetDrawingMode(false);
                else
                {
                    DrawingCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    BtnPen.Background = BrushBtnDark;
                    BtnErase.Background = BrushAccent;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  CLEAR
        // ═══════════════════════════════════════════
        private async void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            try { DrawingCanvas.Strokes.Clear(); }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  색상 버튼
        // ═══════════════════════════════════════════
        private async void ColorBtn_Click(object sender, RoutedEventArgs e)
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
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  굵기 슬라이더
        // ═══════════════════════════════════════════
        private async void SliderThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                _penThickness = e.NewValue;
                if (TxtThickness == null) return;
                TxtThickness.Text = ((int)_penThickness).ToString();
                ApplyDrawingAttributes();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  RESET
        //  DrawingCanvas 초기화 + VM.Reset() (CommentText/콤보 + 플래그)
        // ═══════════════════════════════════════════
        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DrawingCanvas.Strokes.Clear();
                SetDrawingMode(false);
                VM.Reset();   // CommentText 필드 직접 할당 → IsCommentDirty=false 처리 포함
                _isDirty = false;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  BACK → Scan 화면 복귀
        //  HasUnsavedChanges() → ISF 또는 코멘트 변경 있으면 ConfirmSaveAll() 팝업
        //  팝업은 ViewModel에서 처리 (MVVM 패턴 준수)
        // ═══════════════════════════════════════════
        private async void Back_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (HasUnsavedChanges())
                {
                    bool save = await VM.ConfirmSaveAll();
                    if (save)
                    {
                        VM.SaveComment(DrawingCanvas.Strokes, DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight);
                       
                    }
                    _isDirty = false;
                }

                MainPage.Instance.NavigateTo(new Scan(_patient, _studyId));
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }
    }
}