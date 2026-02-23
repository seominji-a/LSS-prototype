using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LSS_prototype
{
    public partial class CustomMessageWindow : Window
    {
        public enum MessageBoxType
        {
            Ok,
            YesNo,
            AutoClose
        }

        public enum MessageBoxResult
        {
            None,
            Ok,
            Yes,
            No,
            Timeout
        }

        public enum MessageIconType
        {
            None,       // 아이콘 없음
            Info,       // 알림 - 파란 계열
            Warning,    // 주의 - 노란색
            Danger      // 위험 - 빨간색
        }

        private List<Window> _blurredWindows = new List<Window>();
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private TaskCompletionSource<MessageBoxResult> _tcs;
        private DispatcherTimer _timeoutTimer;

        public CustomMessageWindow(
            string message,
            MessageBoxType type = MessageBoxType.Ok,
            int autoCloseSeconds = 0,
            MessageIconType icon = MessageIconType.None)
        {
            InitializeComponent();

            // Owner 설정
            var owner = Application.Current?.Windows?
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
                ?? Application.Current?.Windows?
                .OfType<Window>()
                .FirstOrDefault(w => w.IsVisible);

            if (owner != null && owner.IsVisible)
            {
                this.Owner = owner;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            MessageText.Text = message;
            CountdownText.Visibility = Visibility.Collapsed;

            // 아이콘 설정
            SetIcon(icon);

            // 블러 효과 적용
            ApplyBlurToAllWindows();

            // 버튼 타입 설정
            switch (type)
            {
                case MessageBoxType.Ok:
                    OkButton.Visibility = Visibility.Visible;
                    YesButton.Visibility = Visibility.Collapsed;
                    NoButton.Visibility = Visibility.Collapsed;
                    break;

                case MessageBoxType.YesNo:
                    OkButton.Visibility = Visibility.Collapsed;
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;

                    if (autoCloseSeconds > 0)
                        StartTimeout(autoCloseSeconds);
                    break;

                case MessageBoxType.AutoClose:
                    OkButton.Visibility = Visibility.Collapsed;
                    YesButton.Visibility = Visibility.Collapsed;
                    NoButton.Visibility = Visibility.Collapsed;

                    if (autoCloseSeconds > 0)
                    {
                        var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(autoCloseSeconds)
                        };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            CloseWithResult(MessageBoxResult.Ok);
                        };
                        timer.Start();
                    }
                    break;
            }

            // 창 닫힐 때 블러 제거
            this.Closed += (s, e) => RemoveBlurFromAllWindows();
        }

        private void SetIcon(MessageIconType icon)
        {
            if (icon == MessageIconType.None)
            {
                IconBorder.Visibility = Visibility.Collapsed;
                return;
            }

            IconBorder.Visibility = Visibility.Visible;

            switch (icon)
            {
                case MessageIconType.Info:
                    IconText.Text = "ℹ";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(23, 55, 83)); // #173753
                    break;

                case MessageIconType.Warning:
                    IconText.Text = "⚠";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(234, 179, 8)); // 노란색
                    break;

                case MessageIconType.Danger:
                    IconText.Text = "✖";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // 빨간색
                    break;
            }
        }

        private void ApplyBlurToAllWindows()
        {
            try
            {
                var blurEffect = new BlurEffect { Radius = 10 };

                foreach (Window window in Application.Current.Windows)
                {
                    if (window != this && window.IsVisible)
                    {
                        window.Effect = blurEffect;
                        _blurredWindows.Add(window);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " ApplyBlurToAllWindows Function Check");
            }
        }

        private void RemoveBlurFromAllWindows()
        {
            try
            {
                foreach (var window in _blurredWindows)
                {
                    if (window != null)
                        window.Effect = null;
                }
                _blurredWindows.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " RemoveBlurFromAllWindows Function Check");
            }
        }

        private void StartTimeout(int seconds)
        {
            int remainingSeconds = seconds;

            CountdownText.Text = $"{remainingSeconds}초 남음";
            CountdownText.Visibility = Visibility.Visible;

            _timeoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _timeoutTimer.Tick += (s, e) =>
            {
                remainingSeconds--;

                if (remainingSeconds > 0)
                {
                    CountdownText.Text = $"{remainingSeconds}초 남음";
                }
                else
                {
                    _timeoutTimer.Stop();
                    CloseWithResult(MessageBoxResult.Timeout);
                }
            };

            _timeoutTimer.Start();
        }

        public async Task<MessageBoxResult> ShowAsync()
        {
            _tcs = new TaskCompletionSource<MessageBoxResult>();
            this.Show();
            return await _tcs.Task;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
            => CloseWithResult(MessageBoxResult.Ok);

        private void YesButton_Click(object sender, RoutedEventArgs e)
            => CloseWithResult(MessageBoxResult.Yes);

        private void NoButton_Click(object sender, RoutedEventArgs e)
            => CloseWithResult(MessageBoxResult.No);

        private void CloseWithResult(MessageBoxResult result)
        {
            _timeoutTimer?.Stop();
            Result = result;
            _tcs?.TrySetResult(result);
            this.Close();
        }

        private void Overlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Source is Grid)
            {
                System.Diagnostics.Debug.WriteLine("[CustomMessageWindow] 배경 클릭");
            }
        }

        // 정적 Show (동기)
        public static MessageBoxResult Show(
            string message,
            MessageBoxType type = MessageBoxType.Ok,
            int autoCloseSeconds = 0,
            MessageIconType icon = MessageIconType.None)
        {
            var win = new CustomMessageWindow(message, type, autoCloseSeconds, icon);
            win.ShowDialog();
            return win.Result;
        }

        // 정적 ShowAsync (비동기)
        public static async Task<MessageBoxResult> ShowAsync(
            string message,
            MessageBoxType type = MessageBoxType.Ok,
            int autoCloseSeconds = 0,
            MessageIconType icon = MessageIconType.None)
        {
            var win = new CustomMessageWindow(message, type, autoCloseSeconds, icon);
            return await win.ShowAsync();
        }
    }
}