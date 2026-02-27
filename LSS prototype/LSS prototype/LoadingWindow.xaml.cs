using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

namespace LSS_prototype
{
    public partial class LoadingWindow : Window
    {
        private static LoadingWindow _instance;
        private static CancellationTokenSource _cts;

        public LoadingWindow(string message = "처리 중...")
        {
            InitializeComponent();
            MessageText.Text = message;

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

        /// <summary>
        /// 작업 시작 후 0.5초 이상 작업이 경과되어야지만, 로딩바 호출 ( 무분별한 로딩바 UI 출력 방지 )  
        /// </summary>
        /// <param name="message"></param>
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
                _instance?.Close();
                _instance = null;
            });
        }
    }
}