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

            //   Owner 설정 - 한 번만!
            var owner = Application.Current?.MainWindow;
            if (owner != null && owner != this && owner.IsVisible)
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

            SetIcon(icon);

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

            Loaded += async (s, e) => await ApplyBlurToAllWindows();
            this.Closed += async (s, e) => await RemoveBlurFromAllWindows();
            Loaded += (s, e) => App.ActivityMonitor?.RegisterWindow(this);
        }

        private void SetIcon(MessageIconType icon)
        {
            if (icon == MessageIconType.None)
            {
                IconBorder.Visibility = Visibility.Collapsed;
                return;
            }

            IconBorder.Visibility = Visibility.Visible;
            IconBorder.Background = Brushes.Transparent; // 배경 제거 (아이콘 자체에 색상 포함)

            string pathData;
            string colorHex;

            switch (icon)
            {
                case MessageIconType.Info:
                    // Success_2.svg (체크 아이콘)
                    pathData = "M50,0C22.39,0,0,22.39,0,50s22.39,50,50,50,50-22.39,50-50S77.61,0,50,0ZM83.9,34.48l-38.12,38.12c-.99.99-2.29,1.47-3.6,1.45-1.3.02-2.61-.46-3.6-1.45l-22.48-22.48c-1.94-1.94-1.94-5.13,0-7.07,1.94-1.94,5.13-1.94,7.07,0l19.01,19.01,34.65-34.65c1.94-1.94,5.13-1.94,7.07,0,1.94,1.94,1.94,5.13,0,7.07Z";
                    colorHex = "#173753";
                    break;

                case MessageIconType.Warning:
                    // Warning_1.svg (느낌표 아이콘)
                    pathData = "M50,0C22.39,0,0,22.39,0,50s22.39,50,50,50,50-22.39,50-50S77.61,0,50,0ZM50,83.19c-3.72,0-6.74-3.02-6.74-6.74s3.02-6.74,6.74-6.74,6.74,3.02,6.74,6.74-3.02,6.74-6.74,6.74ZM58.08,25.9l-3.9,33.69c-.64,4.94-7.76,4.9-8.37,0l-3.9-33.69c-.52-4.46,2.68-8.5,7.15-9.02,5.13-.66,9.67,3.9,9.02,9.02Z";
                    colorHex = "#ff7900";
                    break;

                case MessageIconType.Danger:
                    // Error.svg (X 아이콘)
                    pathData = "M50,0C22.39,0,0,22.39,0,50s22.39,50,50,50,50-22.39,50-50S77.61,0,50,0ZM73.54,66.46c1.95,1.95,1.95,5.12,0,7.07-1.95,1.95-5.12,1.95-7.07,0l-16.46-16.46-16.46,16.46c-1.95,1.95-5.12,1.95-7.07,0-1.95-1.95-1.95-5.12,0-7.07l16.46-16.46-16.46-16.46c-1.95-1.95-1.95-5.12,0-7.07,1.95-1.95,5.12-1.95,7.07,0l16.46,16.46,16.46-16.46c1.95-1.95,5.12-1.95,7.07,0,1.95,1.95,1.95,5.12,0,7.07l-16.46,16.46,16.46,16.46Z";
                    colorHex = "#ea224b";
                    break;

                default:
                    return;
            }

            IconText.Visibility = Visibility.Collapsed;
            IconPath.Data = Geometry.Parse(pathData);
            IconPath.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            IconPath.Visibility = Visibility.Visible;
        }

        private async Task ApplyBlurToAllWindows()
        {
            try
            {
                var blurEffect = new BlurEffect { Radius = 10 };

                foreach (Window window in Application.Current.Windows)
                {
                    if (window != this && window.IsVisible)
                    {
                        //   윈도우 전체가 아니라 Content(UIElement)에만 블러
                        if (window.Content is UIElement content)
                        {
                            content.Effect = blurEffect;
                            _blurredWindows.Add(window); // window는 추적용으로만
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task RemoveBlurFromAllWindows()
        {
            try
            {
                foreach (var window in _blurredWindows)
                {
                    if (window?.Content is UIElement content)
                        content.Effect = null;
                }
                _blurredWindows.Clear();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
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
        /*        public static MessageBoxResult Show(
                    string message,
                    MessageBoxType type = MessageBoxType.Ok,
                    int autoCloseSeconds = 0,
                    MessageIconType icon = MessageIconType.None)
                {
                    var win = new CustomMessageWindow(message, type, autoCloseSeconds, icon);
                    win.ShowDialog();
                    return win.Result;
                }*/

        // 정적 ShowAsync (비동기 -> 무조건 모든 메시지창이 비동기여야함 why? 세션이 UI 단에서 동작하므로 별도로 관리해야함 )
        public static async Task<MessageBoxResult> ShowAsync(
            string message,
            MessageBoxType type = MessageBoxType.Ok,
            int autoCloseSeconds = 0,
            MessageIconType icon = MessageIconType.Info)
        {
            var win = new CustomMessageWindow(message, type, autoCloseSeconds, icon);
            return await win.ShowAsync();
        }
    }
}