using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Data.SQLite;
using LSS_prototype.DB_CRUD;

namespace LSS_prototype.Login_Page
{
    /// <summary>
    /// Login.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
            var db = new DB_Manager();
            db.InitDB();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void PasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarning.Visibility =
                (Console.CapsLock && txtPassword.IsKeyboardFocusWithin)? Visibility.Visible: Visibility.Collapsed;
        }

        /// <summary>
        /// 패스워드 박스 클릭 시 ADMIN 권한을 가진 ID 리스트들과 현재 ID 박스에 있는 아이디와 비교하여
        /// 리스트 내에 존재한다면 어드민 박스를 사용자에게 출력한다.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PasswordBox_CheckCaps(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                var vm = DataContext as LoginViewModel;
                if (vm != null)
                    vm.UpdateAdminModeVisibilityByUserId();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "PasswordBox_CheckCaps function Check");
            }
           
        }
    }
}
