using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LSS_prototype
{
    public interface IDialogService
    {
        Task<bool?> ShowDialogAsync(object viewModel); // 세션 안 멈추는 비동기 버전
        Task ShowSetting();
        Task ShowDefault();
    }

    internal class Dialog : IDialogService
    {
        public async Task<bool?> ShowDialogAsync(object viewModel)
        {
            var tcs = new TaskCompletionSource<bool?>();
            var blurredWindows = new List<Window>();
            var window = CreateWindow(viewModel, tcs);

            window.Closed += async (s, e) =>
            {
                await RemoveBlur(blurredWindows);
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            };

            await ApplyBlur(window, blurredWindows);
            window.Show();
            return await tcs.Task;
        }

        private Window CreateWindow(object viewModel, TaskCompletionSource<bool?> tcs = null)
        {
            var window = new Window
            {
                Content = viewModel,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

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

            window.DataContext = viewModel;
            return window;
        }

        private Window CreateWindow(object viewModel)
        {
            var window = new Window
            {
                Content = viewModel,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            try
            {
                var vm = viewModel as dynamic;
                vm.CloseAction = new Action<bool?>((result) =>
                {
                    try { window.DialogResult = result; }
                    catch (InvalidOperationException) { }
                    window.Close();
                });
            }
            catch { }

            window.DataContext = viewModel;
            return window;
        }

        private async Task ApplyBlur(Window target, List<Window> blurredWindows)
        {
            try
            {
                var blurEffect = new BlurEffect { Radius = 10 };
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != target && window.IsVisible)
                    {
                        window.Effect = blurEffect;
                        blurredWindows.Add(window);
                    }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task RemoveBlur(List<Window> blurredWindows)
        {
            try
            {
                foreach (var window in blurredWindows)
                {
                    if (window != null)
                        window.Effect = null;
                }
                blurredWindows.Clear();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ══════════════════════════════════════════
        //  ShowSetting() / ShowDefault()
        //  ShowDialog() → Show() + TaskCompletionSource
        //  → UI 스레드 안 막음 → 세션 타이머 계속 동작
        // ══════════════════════════════════════════
        public async Task ShowSetting()
        {
            var tcs = new TaskCompletionSource<bool?>();
            var blurredWindows = new List<Window>();

            try
            {
                var window = new setting();
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                window.Closed += async (s, e) =>
                {
                    await RemoveBlur(blurredWindows);
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(null);
                };

                await ApplyBlur(window, blurredWindows);
                window.Show(); // ShowDialog() → Show() 로 변경 (UI 스레드 블록 방지)
                await tcs.Task;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await RemoveBlur(blurredWindows);
            }
        }

        public async Task ShowDefault()
        {
            var tcs = new TaskCompletionSource<bool?>();
            var blurredWindows = new List<Window>();

            try
            {
                var window = new Default();
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                window.Closed += async (s, e) =>
                {
                    await RemoveBlur(blurredWindows);
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(null);
                };

                await ApplyBlur(window, blurredWindows);
                window.Show(); // ShowDialog() → Show() 로 변경 (UI 스레드 블록 방지)
                await tcs.Task;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await RemoveBlur(blurredWindows);
            }
        }
    }
}
