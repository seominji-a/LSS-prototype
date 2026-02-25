using System.Windows;
using System.Windows.Controls;

namespace LSS_prototype.User_Page
{
    public partial class User : UserControl
    {
        public User()
        {
            InitializeComponent();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());
        }
            
    }
}