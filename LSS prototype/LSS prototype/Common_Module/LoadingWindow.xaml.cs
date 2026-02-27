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
            //MessageText.Text = message;

            var owner = Application.Current?.Windows?
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
                ?? Application.Current?.Windows?
                .OfType<Window>()
                .FirstOrDefault(w => w.IsVisible);

            if (owner != null && owner != this)
            {
                this.Owner = owner;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }

        public static void Begin(string message = "처리 중...")
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Delay(500, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_instance != null) return;
                    ApplyBlur();
                    _instance = new LoadingWindow(message);
                    _instance.Show();
                });
            });
        }

        public static void End()
        {
            _cts?.Cancel();
            _cts = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                RemoveBlur();
                _instance?.Close();
                _instance = null;
            });
        }

        private static void ApplyBlur()
        {
            var blurEffect = new BlurEffect { Radius = 3 }; // 아주 살짝만
            foreach (Window w in Application.Current.Windows)
            {
                if (w is LoadingWindow || !w.IsVisible) continue;
                w.Effect = blurEffect;
                _blurredWindows.Add(w);
            }
        }

        private static void RemoveBlur()
        {
            foreach (var w in _blurredWindows)
            {
                if (w != null) w.Effect = null;
            }
            _blurredWindows.Clear();
        }
    }
}