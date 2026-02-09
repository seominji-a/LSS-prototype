using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent // 배경 투명 (Border로 디자인)
            };

            window.DataContext = viewModel;
            return window.ShowDialog();
        }
    }
}
