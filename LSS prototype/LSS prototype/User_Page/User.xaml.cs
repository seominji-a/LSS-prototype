using LSS_prototype.User_Page;
using System.Windows;


namespace LSS_prototype
{
    /// <summary>
    /// User.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class User : Window
    {
        public User()
        {
            InitializeComponent();
            DataContext = new UserViewModel();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            Patient patientWindow = new Patient();
            patientWindow.Show();
            this.Close();
        }
    }
}
