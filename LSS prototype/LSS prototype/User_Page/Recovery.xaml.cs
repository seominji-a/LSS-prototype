using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Threading;
using System.Windows;

namespace LSS_prototype.User_Page
{
    public partial class Recovery : UserControl
    {
        private RecoveryViewModel VM => DataContext as RecoveryViewModel;


        private string _lastSearchText = string.Empty;

        // ── 재생 상태 (UI 전용) ──
        private bool _isPlaying = false;
        private bool _isDraggingSeek = false;

        // ── 100ms 타이머: 슬라이더 + 경과시간 갱신 ──
        private readonly DispatcherTimer _timer;

        public Recovery()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += Timer_Tick;

            // Patient.xaml.cs 와 동일
            txtSearch.TextChanged += OnSearchTextChanged;

            Loaded += (s, e) =>
            {
                if (DataContext is RecoveryViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        // ── PreviewVideoPath 변경 → MediaElement 제어 ──
                        if (args.PropertyName == nameof(RecoveryViewModel.PreviewVideoPath))
                            OnPreviewVideoPathChanged(vm);

                        // ── CurrentStrokes 변경 → InkCanvas 스케일 변환 후 세팅 ──
                        if (args.PropertyName == nameof(RecoveryViewModel.CurrentStrokes))
                            ApplyStrokesWithScale(vm);
                    };
                }
            };

            Unloaded += (s, e) =>
            {
                _timer.Stop();
                PreviewVideo.Stop();
                PreviewVideo.Source = null;
            };
        }

        // Patient.xaml 검색로직과 동일 
        public void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string current = txtSearch.Text;
            if (current == _lastSearchText) return;

            _lastSearchText = current;

            if (DataContext is RecoveryViewModel vm)
                vm.OnSearchTextChanged(current);
        }

        // ═══════════════════════════════════════════
        //  PreviewVideoPath 변경 감지
        //  경로 있음 → 2배속 기본값으로 로드 + 재생
        //  경로 없음 → 정지 + 초기화
        // ═══════════════════════════════════════════
        private void OnPreviewVideoPathChanged(RecoveryViewModel vm)
        {
            try
            {
                if (!string.IsNullOrEmpty(vm.PreviewVideoPath))
                {
                    PreviewVideo.Source = new Uri(vm.PreviewVideoPath);
                    PreviewVideo.SpeedRatio = 2.0;
                    PreviewVideo.Play();

                    _isPlaying = true;
                    TxtPlayPauseIcon.Text = "⏸";
                    SliderSeek.Value = 0;
                    TxtCurrentTime.Text = "00:00";
                    TxtTotalTime.Text = "00:00";

                    _timer.Start();
                }
                else
                {
                    StopAndReset();
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  MediaElement 이벤트
        // ═══════════════════════════════════════════

        private void PreviewVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!PreviewVideo.NaturalDuration.HasTimeSpan) return;

                var total = PreviewVideo.NaturalDuration.TimeSpan;
                TxtTotalTime.Text = total.ToString(@"mm\:ss");
                SliderSeek.Maximum = total.TotalSeconds;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void PreviewVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            try
            {
                PreviewVideo.Position = TimeSpan.Zero;
                PreviewVideo.Play();
                PreviewVideo.SpeedRatio = 2.0;
                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  타이머 Tick → 슬라이더 + 경과시간 갱신
        // ═══════════════════════════════════════════
        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isDraggingSeek) return;
                if (PreviewVideo?.Source == null) return;
                if (!PreviewVideo.NaturalDuration.HasTimeSpan) return;

                SliderSeek.Value = PreviewVideo.Position.TotalSeconds;
                TxtCurrentTime.Text = PreviewVideo.Position.ToString(@"mm\:ss");
            }
            catch { }
        }

        // ═══════════════════════════════════════════
        //  버튼 이벤트
        // ═══════════════════════════════════════════

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PreviewVideo.Source == null) return;

                if (_isPlaying)
                {
                    PreviewVideo.Pause();
                    _isPlaying = false;
                    TxtPlayPauseIcon.Text = "▶";
                    _timer.Stop();
                }
                else
                {
                    PreviewVideo.Play();
                    PreviewVideo.SpeedRatio = 2.0;
                    _isPlaying = true;
                    TxtPlayPauseIcon.Text = "⏸";
                    _timer.Start();
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  Seek 슬라이더
        // ═══════════════════════════════════════════
        private void PreviewSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSeek = true;
        }

        private void PreviewSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                PreviewVideo.Position = TimeSpan.FromSeconds(SliderSeek.Value);

                // 정지 상태에서도 프레임 갱신
                if (!_isPlaying)
                {
                    PreviewVideo.Play();
                    PreviewVideo.Pause();
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
            finally
            {
                _isDraggingSeek = false;
            }
        }

        private void PreviewSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSeek && TxtCurrentTime != null)
                TxtCurrentTime.Text =
                    TimeSpan.FromSeconds(SliderSeek.Value).ToString(@"mm\:ss");
        }

        // ═══════════════════════════════════════════
        //  정지 + UI 초기화
        // ═══════════════════════════════════════════
        private void StopAndReset()
        {
            try
            {
                _timer.Stop();
                PreviewVideo.Stop();
                PreviewVideo.Source = null;

                _isPlaying = false;
                _isDraggingSeek = false;
                TxtPlayPauseIcon.Text = "▶";
                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
                TxtTotalTime.Text = "00:00";
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ISF 스케일 변환
        // ═══════════════════════════════════════════
        private void ApplyStrokesWithScale(RecoveryViewModel vm)
        {
            try
            {
                var strokes = vm.CurrentStrokes ?? new StrokeCollection();

                if (strokes.Count == 0)
                {
                    PreviewDrawingCanvas.Strokes = strokes;
                    return;
                }

                double currentWidth = PreviewDrawingCanvas.ActualWidth;
                double currentHeight = PreviewDrawingCanvas.ActualHeight;

                if (currentWidth <= 0 || currentHeight <= 0)
                {
                    SizeChangedEventHandler handler = null;
                    handler = (s, ev) =>
                    {
                        PreviewDrawingCanvas.SizeChanged -= handler;
                        ApplyStrokesWithScale(vm);
                    };
                    PreviewDrawingCanvas.SizeChanged += handler;
                    return;
                }

                double originalWidth = vm.OriginalCanvasWidth;
                double originalHeight = vm.OriginalCanvasHeight;

                if (originalWidth <= 0 || originalHeight <= 0)
                {
                    PreviewDrawingCanvas.Strokes = strokes;
                    return;
                }

                double imgRatio = vm.PreviewImageWidth / vm.PreviewImageHeight;
                double viewerRatio = currentWidth / currentHeight;

                double renderedW, renderedH, offsetX, offsetY;
                if (imgRatio > viewerRatio)
                {
                    renderedW = currentWidth;
                    renderedH = currentWidth / imgRatio;
                    offsetX = 0;
                    offsetY = (currentHeight - renderedH) / 2;
                }
                else
                {
                    renderedH = currentHeight;
                    renderedW = currentHeight * imgRatio;
                    offsetX = (currentWidth - renderedW) / 2;
                    offsetY = 0;
                }

                double scaleX = renderedW / originalWidth;
                double scaleY = renderedH / originalHeight;

                var cloned = strokes.Clone();
                foreach (var stroke in cloned)
                {
                    var matrix = new System.Windows.Media.Matrix();
                    matrix.Scale(scaleX, scaleY);
                    matrix.Translate(offsetX, offsetY);
                    stroke.Transform(matrix, false);
                }

                PreviewDrawingCanvas.Strokes = cloned;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
    }
}