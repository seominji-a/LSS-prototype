using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private List<Window> _blurredWindows = new List<Window>();
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private TaskCompletionSource<MessageBoxResult> _tcs;
        private DispatcherTimer _timeoutTimer;

        public CustomMessageWindow(string message, MessageBoxType type = MessageBoxType.Ok, int autoCloseSeconds = 0)
        {
            InitializeComponent();

            // Owner 설정 (DB 초기화 단계 고려)
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

            //  블러 효과 적용
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
                    {
                        StartTimeout(autoCloseSeconds);
                    }
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

            //  창 닫힐 때 블러 제거
            this.Closed += (s, e) =>
            {
                RemoveBlurFromAllWindows();
            };
        }

        //  모든 창에 블러 효과 적용
        private void ApplyBlurToAllWindows()
        {
            var blurEffect = new BlurEffect
            {
                Radius = 10  // 블러 강도
            };

            foreach (Window window in Application.Current.Windows)
            {
                if (window != this && window.IsVisible)
                {
                    window.Effect = blurEffect;
                    _blurredWindows.Add(window);
                }
            }
        }

        //  모든 창에서 블러 효과 제거
        private void RemoveBlurFromAllWindows()
        {
            foreach (var window in _blurredWindows)
            {
                if (window != null)
                {
                    window.Effect = null;
                }
            }
            _blurredWindows.Clear();
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
        {
            CloseWithResult(MessageBoxResult.Ok);
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(MessageBoxResult.Yes);
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(MessageBoxResult.No);
        }

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
                System.Diagnostics.Debug.WriteLine("[CustomMessageWindow] 배경 클릭 - 세션 연장됨");
            }
        }

        // 정적 헬퍼 메서드
        public static MessageBoxResult Show(string message, MessageBoxType type = MessageBoxType.Ok, int autoCloseSeconds = 0)
        {
            var win = new CustomMessageWindow(message, type, autoCloseSeconds);
            win.ShowDialog();
            return win.Result;
        }
    }
}