using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Effects;

namespace LSS_prototype
{
    public partial class LoadingWindow : Window
    {
        private static LoadingWindow _instance;
        private static CancellationTokenSource _cts;
        private static readonly List<Window> _blurredWindows = new List<Window>();

        public LoadingWindow(string message = "처리 중...")
        {
            InitializeComponent();

            // ✅ MainWindow 고정 + IsVisible 체크
            // IsVisible 없으면 아직 Show() 안 된 창을 Owner로 설정 시 에러 발생
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

            // ✅ 로딩창 열리는 즉시 세션 모니터에 등록
            // → 로딩창 위에서 마우스 움직여도 세션 연장됨
            Loaded += (s, e) => App.ActivityMonitor?.RegisterWindow(this);
        }

        public static void Begin(string message = "처리 중...")
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Delay(500, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                // ✅ Invoke(동기) → BeginInvoke(비동기)
                // → UI 스레드 블로킹 없음 → 세션 타이머 정상 동작
                Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (_instance != null) return;
                    ApplyBlur();
                    _instance = new LoadingWindow(message);
                    _instance.Show();
                }));
            });
        }

        public static void Update(string message)
        {
            // ✅ Invoke → BeginInvoke
            Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (_instance != null)
                    _instance.MessageText.Text = message;
            }));
        }

        public static void End()
        {
            _cts?.Cancel();
            _cts = null;

            // ✅ End는 Invoke 유지 (순서 보장 필요)
            // BeginInvoke로 바꾸면 Begin의 창 생성보다 먼저 실행되서
            // 창이 뜨자마자 바로 닫히는 문제 발생
            Application.Current.Dispatcher.Invoke(() =>
            {
                RemoveBlur();
                _instance?.Close();
                _instance = null;
            });
        }

        private static void ApplyBlur()
        {
            var blurEffect = new BlurEffect { Radius = 3 };
            foreach (Window w in Application.Current.Windows)
            {
                if (w is LoadingWindow || !w.IsVisible) continue;

                // ✅ w.Effect → w.Content.Effect
                // → 윈도우 레벨 타이머(세션) 영향 없음
                if (w.Content is System.Windows.UIElement content)
                {
                    content.Effect = blurEffect;
                    _blurredWindows.Add(w);
                }
            }
        }

        private static void RemoveBlur()
        {
            foreach (var w in _blurredWindows)
            {
                if (w?.Content is System.Windows.UIElement content)
                    content.Effect = null;
            }
            _blurredWindows.Clear();
        }
    }
}