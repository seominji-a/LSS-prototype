using LSS_prototype.User_Page;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LSS_prototype
{
    public interface IDialogService
    {
        Task<bool?> ShowDialogAsync(object viewModel);
        Task ShowSetting();
        Task ShowDefault();
    }

    internal class Dialog : IDialogService
    {
        // ══════════════════════════════════════════
        //  ShowDialogAsync
        //  Show() + TaskCompletionSource
        //  → UI 스레드 안 막음 → 세션 타이머 계속 동작
        //  뒷배경 터치 차단은 각 XAML의 반투명 오버레이가 담당
        // ══════════════════════════════════════════
        public async Task<bool?> ShowDialogAsync(object viewModel)
        {
            var tcs = new TaskCompletionSource<bool?>();
            var window = CreateWindow(viewModel, tcs);

            window.Closed += (s, e) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            };

            window.Show();

            // 각 Window의 Loaded에서도 등록되지만 혹시 모를 경우 대비)
            App.ActivityMonitor?.RegisterWindow(window);

            return await tcs.Task;
        }

        private Window CreateWindow(object viewModel, TaskCompletionSource<bool?> tcs = null)
        {
            // ✅ 각 Dialog는 이미 Window(Maximized + 반투명 오버레이)로 만들어져 있음
            // Content만 설정하지 않고, 각 Dialog XAML 자체가 Window임
            // → viewModel을 DataContext로 받아서 CloseAction 연결만 해줌
            Window window = null;

            // viewModel 타입에 따라 해당 Window 생성
            if (viewModel is Patient_Page.PatientAddViewModel)
                window = new Patient_Page.PatientAddDialog { DataContext = viewModel };
            else if (viewModel is Patient_Page.PatientEditViewModel)
                window = new Patient_Page.PatientEditDialog { DataContext = viewModel };
            else if (viewModel is User_AddViewModel)
                window = new User_Add { DataContext = viewModel };
            else if (viewModel is User_EditViewModel)
                window = new User_Edit { DataContext = viewModel };
            else
            {
                // 그 외 ViewModel은 기존 방식으로 처리
                window = new Window
                {
                    Content = viewModel,
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize,
                    Topmost = true
                };
                window.DataContext = viewModel;
            }

            // CloseAction 연결
            try
            {
                var vm = viewModel as dynamic;
                vm.CloseAction = new Action<bool?>((result) =>
                {
                    tcs?.TrySetResult(result);
                    window.Close();
                });
            }
            catch { }

            return window;
        }

        // ══════════════════════════════════════════
        //  ShowSetting / ShowDefault
        //  setting, Default 창은 기존 Window 그대로 사용
        //  (별도 XAML 수정 필요 시 동일한 방식으로 변경 가능)
        // ══════════════════════════════════════════
        public async Task ShowSetting()
        {
            var tcs = new TaskCompletionSource<bool?>();

            try
            {
                var window = new setting();
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Topmost = true;

                window.Closed += (s, e) =>
                {
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(null);
                };

                App.ActivityMonitor?.RegisterWindow(window);
                window.Show();
                await tcs.Task;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        public async Task ShowDefault()
        {
            var tcs = new TaskCompletionSource<bool?>();

            try
            {
                var window = new Default();
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Topmost = true;

                window.Closed += (s, e) =>
                {
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(null);
                };

                App.ActivityMonitor?.RegisterWindow(window);
                window.Show();
                await tcs.Task;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }
    }
}