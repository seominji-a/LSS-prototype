using System.Windows;
using System.Windows.Controls;

namespace LSS_prototype.User_Page
{
    public partial class User : UserControl
    {
        public User()
        {
            InitializeComponent();

            var vm = new UserViewModel();
            DataContext = vm;
            Unloaded += (s, e) => (DataContext as UserViewModel)?.Dispose();
            Loaded += async (s, e) => await vm.InitializeAsync();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());
        }
    }
}