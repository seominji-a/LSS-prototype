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
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true
            };

            window.DataContext = viewModel;
            return window.ShowDialog();
        }
    }
}
