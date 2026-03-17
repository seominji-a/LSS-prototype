using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace LSS_prototype.User_Page
{
    /// <summary>
    /// Recovery.xaml 코드비하인드
    /// MediaElement, InkCanvas 조작만 담당, 나머지 로직은 RecoveryViewModel
    /// </summary>
    public partial class Recovery : UserControl
    {
        private RecoveryViewModel VM => DataContext as RecoveryViewModel;

        public Recovery()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (DataContext is RecoveryViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        // ── PreviewVideoPath 변경 감지 → MediaElement 직접 제어 ──
                        if (args.PropertyName == nameof(RecoveryViewModel.PreviewVideoPath))
                        {
                            if (!string.IsNullOrEmpty(vm.PreviewVideoPath))
                            {
                                PreviewVideo.Source = new Uri(vm.PreviewVideoPath);
                                PreviewVideo.Play();
                            }
                            else
                            {
                                PreviewVideo.Stop();
                                PreviewVideo.Source = null;
                            }
                        }

                        // ── CurrentStrokes 변경 감지 → InkCanvas.Strokes 갱신 ──
                        if (args.PropertyName == nameof(RecoveryViewModel.CurrentStrokes))
                        {
                            ApplyStrokesWithScale(vm);
                        }
                    };
                }
            };
        }

        // ═══════════════════════════════════════════
        //  ISF Strokes 를 현재 뷰어 크기에 맞게 스케일 변환 후 InkCanvas 에 적용
        // ═══════════════════════════════════════════
        // 이미지 실제 렌더링 영역 계산 (Stretch=Uniform 여백 고려)
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
                    handler = (s, e) =>
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

                // ★ 이미지 실제 표시 영역 계산 (Stretch=Uniform 여백 고려)
                // 원본 이미지 비율
                double imgRatio = vm.PreviewImageWidth / vm.PreviewImageHeight;
                // 현재 뷰어 비율
                double viewerRatio = currentWidth / currentHeight;

                double renderedW, renderedH, offsetX, offsetY;

                if (imgRatio > viewerRatio)
                {
                    // 좌우가 꽉 참 → 위아래 여백
                    renderedW = currentWidth;
                    renderedH = currentWidth / imgRatio;
                    offsetX = 0;
                    offsetY = (currentHeight - renderedH) / 2;
                }
                else
                {
                    // 위아래가 꽉 참 → 좌우 여백
                    renderedH = currentHeight;
                    renderedW = currentHeight * imgRatio;
                    offsetX = (currentWidth - renderedW) / 2;
                    offsetY = 0;
                }

                // 원본도 동일하게 계산
                double origImgRatio = vm.PreviewImageWidth / vm.PreviewImageHeight;
                double origViewRatio = originalWidth / originalHeight;
                double origRenderedW, origRenderedH, origOffsetX, origOffsetY;

                if (origImgRatio > origViewRatio)
                {
                    origRenderedW = originalWidth;
                    origRenderedH = originalWidth / origImgRatio;
                    origOffsetX = 0;
                    origOffsetY = (originalHeight - origRenderedH) / 2;
                }
                else
                {
                    origRenderedH = originalHeight;
                    origRenderedW = originalHeight * origImgRatio;
                    origOffsetX = (originalWidth - origRenderedW) / 2;
                    origOffsetY = 0;
                }

                // 실제 이미지 영역 기준 스케일
                double scaleX = renderedW / origRenderedW;
                double scaleY = renderedH / origRenderedH;

                // 변환: 원본 오프셋 제거 → 스케일 → 현재 오프셋 적용
                var matrix = new System.Windows.Media.Matrix(scaleX, 0, 0, scaleY,
                    -origOffsetX * scaleX + offsetX,
                    -origOffsetY * scaleY + offsetY);

                var scaledStrokes = strokes.Clone();
                scaledStrokes.Transform(matrix, false);

                PreviewDrawingCanvas.Strokes = scaledStrokes;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ── MediaElement 열렸을 때 자동 재생 ──
        private void PreviewVideo_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            try { PreviewVideo.Play(); }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
    }
}