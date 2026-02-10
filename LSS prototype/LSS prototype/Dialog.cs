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
    }
    internal class Dialog : IDialogService
    {
        public bool? ShowDialog(object viewModel)
        {
            var window = new Window
            {
                Content = viewModel,
                Width = 550,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };
            var vm = viewModel as dynamic;

            try
            {
                // 뷰모델에 CloseAction이 있는지 확인하고 연결합니다.
                vm.CloseAction = new Action<bool?>((result) =>
                {
                    window.DialogResult = result;
                    window.Close();
                });
            }
            catch
            {
                // CloseAction이 없는 일반 객체일 경우를 대비해 비워둡니다.
            }


            window.DataContext = viewModel;
            return window.ShowDialog();
        }
    }
}
