using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.Login_Page
{
    public partial class SessionLogin : Window
    {
        public SessionLogin()
        {
            InitializeComponent();
        }

        // 누르는 동안 비밀번호 보임
        private void BtnShowPassword_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPassword.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Visibility = Visibility.Visible;
        }

        // 떼면 다시 숨김
        private void BtnShowPassword_MouseUp(object sender, MouseButtonEventArgs e)
        {
            txtPassword.Visibility = Visibility.Visible;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
        }
    }
}
