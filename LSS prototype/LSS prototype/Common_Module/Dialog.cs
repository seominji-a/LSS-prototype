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
        void ShowSetting();
        void ShowDefault();
    }

    internal class Dialog : IDialogService
    {
        // ═══════════════════════════════════════════
        //  ShowDialogAsync()
        //  Show() 로 UI 스레드 안 막고 띄움
        //  → 세션 타이머 멈추지 않음
        //  블러 처리 추가 → 뒤 화면 터치 차단
        //  확인/취소 결과는 TaskCompletionSource 로 반환
        // ═══════════════════════════════════════════
        public Task<bool?> ShowDialogAsync(object viewModel)
        {
            var tcs = new TaskCompletionSource<bool?>();
            var blurredWindows = new List<Window>();
            var window = CreateWindow(viewModel, tcs); 

            window.Closed += (s, e) =>
            {
                RemoveBlur(blurredWindows);
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            };

            ApplyBlur(window, blurredWindows);
            window.Show();
            return tcs.Task;
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

        // ═══════════════════════════════════════════
        //  CreateWindow()
        //  공통 Window 생성 + CloseAction 연결
        // ═══════════════════════════════════════════
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

            // CloseAction 연결 (ViewModel 에서 창 닫기용)
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

        // ═══════════════════════════════════════════
        //  ApplyBlur() / RemoveBlur()
        //  CustomMessageWindow 와 완전 동일한 방식
        //  열린 창 전부 블러 → 뒤 화면 터치 차단
        // ═══════════════════════════════════════════
        private void ApplyBlur(Window target, List<Window> blurredWindows)
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
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void RemoveBlur(List<Window> blurredWindows)
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
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ShowSetting() / ShowDefault()
        //  블러 처리 추가
        // ═══════════════════════════════════════════
        public void ShowSetting()
        {
            var blurredWindows = new List<Window>();
            try
            {
                var owner = Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w.IsActive && !(w is setting));

                var window = new setting();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (owner != null) window.Owner = owner;

                ApplyBlur(window, blurredWindows);
                window.Closed += (s, e) => RemoveBlur(blurredWindows);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                RemoveBlur(blurredWindows);
            }
        }

        public void ShowDefault()
        {
            var blurredWindows = new List<Window>();
            try
            {
                var owner = Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w.IsActive && !(w is Default));

                var window = new Default();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (owner != null) window.Owner = owner;

                ApplyBlur(window, blurredWindows);
                window.Closed += (s, e) => RemoveBlur(blurredWindows);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                RemoveBlur(blurredWindows);
            }
        }
    }
}