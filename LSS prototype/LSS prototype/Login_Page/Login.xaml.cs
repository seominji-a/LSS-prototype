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
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
            var db = new DB_Manager();
            db.InitDB();
            (DataContext as LoginViewModel)?.LoadAdminIds(); // 2. 그 다음 호출
        }

        // ── EXIT 버튼 ──
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ── 캡스락 체크 (GotKeyboardFocus + PreviewKeyUp 공용) ──
        private void PasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as LoginViewModel;
                if (vm != null)
                    vm.UpdateAdminModeVisibilityByUserId();

                // 캡스락 체크 — 포커스 진입 즉시
                CapsLockWarning.Visibility =
                    (Console.CapsLock && txtPassword.IsKeyboardFocusWithin)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // ── 포커스 떠날 때 경고 숨김 ──
        private void PasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarning.Visibility = Visibility.Collapsed;
        }

   

        
    }
}