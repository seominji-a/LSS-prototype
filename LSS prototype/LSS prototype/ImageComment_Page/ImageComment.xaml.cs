using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LSS_prototype.ImageComment_Page
{
    public partial class ImageComment : Window
    {
        // ── 드로잉 속성 ──
        private Color _penColor = Color.FromRgb(0xFF, 0x44, 0x44);
        private double _penThickness = 4.0;

        // ── 드로잉 변경 여부 (ISF 로드 직후엔 false, 실제 획 추가/삭제 시 true)
        private bool _isDirty = false;

        private static readonly SolidColorBrush BrushAccent = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        private static readonly SolidColorBrush BrushBtnDark = new SolidColorBrush(Color.FromRgb(0x2A, 0x3F, 0x55));


        private static readonly string[] ImageNames = { "sample", "sample2" };
        private int _currentIndex = 0;

        private string ImageDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image");
        private string IsfDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "isf");

        public ImageComment()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // isf 폴더 없으면 미리 생성
                Directory.CreateDirectory(IsfDir);

                _currentIndex = 0;
                LoadPage(_currentIndex);

                DrawingCanvas.EditingMode = InkCanvasEditingMode.None;
                ApplyDrawingAttributes();
                DrawingCanvas.PreviewMouseDown += DrawingCanvas_PreviewMouseDown;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        //  페이지 로드 (이미지 + ISF)
        private void LoadPage(int index)
        {
            string baseName = ImageNames[index];

            // ── 이미지 로드 ──
            string pngPath = Path.Combine(ImageDir, baseName + ".png");
            if (!File.Exists(pngPath))
            {
                Common.WriteLog(new FileNotFoundException($"이미지 파일 없음: {pngPath}"));
                CapturedImage.Source = null;
            }
            else
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(pngPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                CapturedImage.Source = bmp;
            }

            // ── ISF 로드 (없으면 빈 StrokeCollection) ──
            string isfPath = Path.Combine(IsfDir, baseName + ".isf");

            // StrokesChanged 기존 구독 해제 (페이지 전환 시 중복 방지)
            DrawingCanvas.Strokes.StrokesChanged -= OnStrokesChanged;

            if (File.Exists(isfPath))
            {
                using (var fs = File.OpenRead(isfPath))
                    DrawingCanvas.Strokes = new StrokeCollection(fs);
            }
            else
            {
                DrawingCanvas.Strokes = new StrokeCollection();
            }

            // 로드 직후 → 변경 없음
            _isDirty = false;

            // 새 StrokeCollection 에 이벤트 구독
            DrawingCanvas.Strokes.StrokesChanged += OnStrokesChanged;
        }

        // ────────────────────────────────────────────
        //  현재 페이지 ISF 저장
        // ────────────────────────────────────────────
        private void SaveCurrentIsf()
        {
            string baseName = ImageNames[_currentIndex];
            string isfPath = Path.Combine(IsfDir, baseName + ".isf");

            if (DrawingCanvas.Strokes.Count == 0)
            {
                // 드로잉 없으면 ISF 파일 삭제 (깔끔하게)
                if (File.Exists(isfPath)) File.Delete(isfPath);
                return;
            }

            using (var fs = File.Create(isfPath))
                DrawingCanvas.Strokes.Save(fs);
        }

        // ────────────────────────────────────────────
        //  실제 획 추가/삭제 시 dirty 플래그 ON ( 드로잉이 그려져있어도 추가 드로잉이 없으면 y/n 없이 바로 페이지 이동 ) 
        // ────────────────────────────────────────────
        private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            _isDirty = true;
        }

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

        // ────────────────────────────────────────────
        //  좌/우 20% 터치  [최상단 - try-catch]
        //  - None 모드일 때만 동작
        //  - 드로잉 있으면 저장 여부 확인 후 페이지 전환
        // ────────────────────────────────────────────
        private void DrawingCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
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

                int targetIndex = goNext ? _currentIndex + 1 : _currentIndex - 1;

                // 범위 체크
                if (targetIndex < 0 || targetIndex >= ImageNames.Length)
                {
                    CustomMessageWindow.Show(
                        goNext ? "마지막 이미지입니다." : "첫 번째 이미지입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose,
                        autoCloseSeconds: 1,
                        icon: CustomMessageWindow.MessageIconType.Info);
                    return;
                }

                // 실제로 새 드로잉 작업을 했을 때만 저장 여부 확인
                if (_isDirty)
                {
                    var result = CustomMessageWindow.Show(
                        "드로잉을 저장하시겠습니까?",
                        CustomMessageWindow.MessageBoxType.YesNo,
                        icon: CustomMessageWindow.MessageIconType.Info);

                    if (result == CustomMessageWindow.MessageBoxResult.Yes)
                        SaveCurrentIsf();
                    // No 선택 시 → 저장 안 하고 그냥 이동 (ISF 파일 유지)
                }

                // 페이지 전환
                _currentIndex = targetIndex;
                LoadPage(_currentIndex);
                SetDrawingMode(false); // 이동 후 펜 모드 해제
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            try { SetDrawingMode(DrawingCanvas.EditingMode != InkCanvasEditingMode.Ink); }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

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

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            try { DrawingCanvas.Strokes.Clear(); }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

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

        private void BtnImageDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CapturedImage.Source = null;
                DrawingCanvas.Strokes.Clear();
                SetDrawingMode(false);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DrawingCanvas.Strokes.Clear();
                TxtComment.Text = string.Empty;
                CbPosition.SelectedIndex = -1;
                CbAnatomical.SelectedIndex = -1;
                SetDrawingMode(false);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }


        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try { this.Close(); }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            try { }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
    }
}