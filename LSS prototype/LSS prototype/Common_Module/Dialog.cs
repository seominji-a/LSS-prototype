using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LSS_prototype
{
    public interface IDialogService
    {
        bool? ShowDialog(object viewModel);
        void ShowSetting();
        void ShowDefault();

    }
    internal class Dialog : IDialogService
    {
        public bool? ShowDialog(object viewModel)
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
            var vm = viewModel as dynamic;

            try
            {
                vm.CloseAction = new Action<bool?>((result) =>
                {
                    try
                    {
                        window.DialogResult = result;
                    }
                    catch (InvalidOperationException)
                    {
                        // Show()로 열린 창이면 DialogResult 설정 안 함
                    }
                    window.Close();
                });
            }
            catch
            {
                // CloseAction이 없는 경우
            }


            window.DataContext = viewModel;
            return window.ShowDialog();
        }
        public void ShowSetting()
        {
            try
            {
                var owner = Application.Current.Windows
                                .OfType<Window>()
                                .FirstOrDefault(w => w.IsActive && !(w is setting));

                var window = new setting();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (owner != null) window.Owner = owner;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        public void ShowDefault()
        {
            try
            {
                var owner = Application.Current.Windows
                                .OfType<Window>()
                                .FirstOrDefault(w => w.IsActive && !(w is Default));

                var window = new Default();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (owner != null) window.Owner = owner;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }
    }
}
