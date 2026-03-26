using System.Windows;

namespace LSS_prototype.User_Page
{
    public partial class setting : Window
    {
        public setting()
        {
            InitializeComponent();
            DataContext = new SettingViewModel();
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingViewModel;
            await vm.InitializeAsync();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
