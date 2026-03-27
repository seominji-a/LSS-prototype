using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LSS_prototype.VideoComment_Page
{
    public partial class VideoComment : UserControl
    {
        // ═══════════════════════════════════════════
        //  코드비하인드 역할
        //  MediaElement UI 조작만 담당
        //  비즈니스 로직/팝업은 전부 ViewModel
        // ═══════════════════════════════════════════

        private bool _isPlaying = false;
        private bool _isDraggingSeek = false;
        private bool _disposed = false;

        private readonly DispatcherTimer _timer;

        private VideoCommentViewModel VM => DataContext as VideoCommentViewModel;

        // ═══════════════════════════════════════════
        //  저장 필요 여부 통합 체크 (ImageComment와 동일한 패턴)
        //  IsCommentDirty 만 체크 (영상은 ISF 없음)
        // ═══════════════════════════════════════════
        private bool HasUnsavedChanges() => VM.IsCommentDirty;

        // ═══════════════════════════════════════════
        //  생성자
        // ═══════════════════════════════════════════
        public VideoComment(PatientModel patient, string studyId, string emrcheck)
        {
            InitializeComponent();
            DataContext = new VideoCommentViewModel(patient, studyId,emrcheck);

            VM.PropertyChanged += OnViewModelPropertyChanged;
            VM.RequestNavigateToScan += () => MainPage.Instance.NavigateTo(new Scan(patient, emrcheck, studyId));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += Timer_Tick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        // ═══════════════════════════════════════════
        //  Loaded
        // ═══════════════════════════════════════════
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                VM.SetMediaActions(
                    seekBack: async () =>
                    {
                        try
                        {
                            if (VideoPlayer.Source == null) return;
                            var target = VideoPlayer.Position - TimeSpan.FromSeconds(1);
                            VideoPlayer.Position = target < TimeSpan.Zero ? TimeSpan.Zero : target;
                        }
                        catch (Exception ex) { await Common.WriteLog(ex); }
                    },
                    seekForward: async () =>
                    {
                        try
                        {
                            if (VideoPlayer.Source == null) return;
                            if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;
                            var total = VideoPlayer.NaturalDuration.TimeSpan;
                            var target = VideoPlayer.Position + TimeSpan.FromSeconds(1);
                            VideoPlayer.Position = target > total ? total : target;
                        }
                        catch (Exception ex) { await Common.WriteLog(ex); }
                    },
                    playPause: async () =>
                    {
                        try
                        {
                            if (VideoPlayer.Source == null) return;
                            if (_isPlaying)
                            {
                                VideoPlayer.Pause();
                                _isPlaying = false;
                                VM.IsPlaying = false;
                                _timer.Stop();
                            }
                            else
                            {
                                VideoPlayer.Play();
                                VideoPlayer.SpeedRatio = VM.CurrentSpeedRatio;
                                _isPlaying = true;
                                VM.IsPlaying = true;
                                _timer.Start();
                            }
                        }
                        catch (Exception ex) { await Common.WriteLog(ex); }
                    }
                );

                await VM.Initialize();

                VideoPlayer.PreviewMouseDown += OnVideoPreviewMouseDown;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  Unloaded
        // ═══════════════════════════════════════════
        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            await Dispose();
        }

        // ═══════════════════════════════════════════
        //  좌/우 20% 터치 → 페이지 이동
        //
        //  ImageComment와 동일한 패턴:
        //  1. CanNavigate() 로 이동 가능 여부 먼저 확인
        //     → 1장이거나 첫/마지막이면 팝업 없이 바로 리턴
        //  2. 이동 가능한 경우에만 HasUnsavedChanges() 확인
        //     → 코멘트 변경이 있으면 ConfirmSaveAll() 팝업 (MVVM 패턴)
        // ═══════════════════════════════════════════
        private async void OnVideoPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (VideoPlayer.ActualWidth <= 0) return;

                double ratio = e.GetPosition(VideoPlayer).X / VideoPlayer.ActualWidth;
                bool goNext = ratio >= 0.80;
                bool goPrev = ratio <= 0.20;
                if (!goNext && !goPrev) return;

                e.Handled = true;

                //   이동 불가면 팝업 없이 바로 리턴 (무한 팝업 방지)
                if (!VM.CanNavigate(goNext)) return;

                // 변경 사항 있으면 저장 여부 확인
                if (HasUnsavedChanges())
                {
                    bool save = await VM.ConfirmSaveAll();
                    if (save)
                    {
                        bool success = await VM.SaveCommentAsync();
                        if (!success) return;
                        VM.ResetDirty();
                    }
                    else
                    {
                        VM.ResetDirty();
                    }
                }

                await VM.ResetSpeed();

                if (goNext)
                    await VM.MoveNext();
                else
                    await VM.MovePrev();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ViewModel 프로퍼티 변경 감지
        // ═══════════════════════════════════════════
        private async void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                switch (e.PropertyName)
                {
                    case nameof(VM.CurrentVideoPath):
                        OnCurrentVideoPathChanged();
                        break;

                    case nameof(VM.CurrentSpeedRatio):
                        VideoPlayer.SpeedRatio = VM.CurrentSpeedRatio;
                        break;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async void OnCurrentVideoPathChanged()
        {
            try
            {
                if (string.IsNullOrEmpty(VM.CurrentVideoPath))
                {
                    await StopAndReset();
                    return;
                }

                VideoPlayer.Source = new Uri(VM.CurrentVideoPath);
                VideoPlayer.SpeedRatio = VM.CurrentSpeedRatio;
                VideoPlayer.Play();
                _isPlaying = true;
                VM.IsPlaying = true;
                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
                TxtTotalTime.Text = "00:00";

                _timer.Start();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  MediaElement 이벤트
        // ═══════════════════════════════════════════

        private async void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;
                var total = VideoPlayer.NaturalDuration.TimeSpan;
                TxtTotalTime.Text = total.ToString(@"mm\:ss");
                SliderSeek.Maximum = total.TotalSeconds;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoPlayer.Position = TimeSpan.Zero;
                VideoPlayer.Play();
                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  타이머 Tick → Seek 슬라이더 + 경과시간 업데이트
        // ═══════════════════════════════════════════
        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isDraggingSeek) return;
                if (VideoPlayer?.Source == null) return;
                if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;

                var pos = VideoPlayer.Position;
                SliderSeek.Value = pos.TotalSeconds;
                TxtCurrentTime.Text = pos.ToString(@"mm\:ss");
            }
            catch { }
        }

        // ═══════════════════════════════════════════
        //  Seek 슬라이더
        // ═══════════════════════════════════════════
        private void SliderSeek_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSeek = true;
        }

        private async void SliderSeek_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                VideoPlayer.Position = TimeSpan.FromSeconds(SliderSeek.Value);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            finally
            {
                _isDraggingSeek = false;
            }
        }

        private void SliderSeek_ValueChanged(object sender,
            System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSeek)
                TxtCurrentTime.Text =
                    TimeSpan.FromSeconds(SliderSeek.Value).ToString(@"mm\:ss");
        }


        // ═══════════════════════════════════════════
        //  헬퍼
        // ═══════════════════════════════════════════
        private async Task StopAndReset()
        {
            try
            {
                _timer.Stop();
                VideoPlayer.Stop();
                VideoPlayer.Close();
                VideoPlayer.Source = null;

                _isPlaying = false;
                _isDraggingSeek = false;
                VM.IsPlaying = false;
                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
                TxtTotalTime.Text = "00:00";
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        public async Task Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;

                if (VM != null)
                    VM.PropertyChanged -= OnViewModelPropertyChanged;

                VideoPlayer.PreviewMouseDown -= OnVideoPreviewMouseDown;
                VideoPlayer.Stop();
                VideoPlayer.Close();
                VideoPlayer.Source = null;

                VideoPlayer.MediaOpened -= VideoPlayer_MediaOpened;
                VideoPlayer.MediaEnded -= VideoPlayer_MediaEnded;

                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }
    }
}