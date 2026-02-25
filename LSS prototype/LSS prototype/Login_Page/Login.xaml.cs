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
        }

        // ── EXIT 버튼 ──
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ── 캡스락 체크 (GotKeyboardFocus + PreviewKeyUp 공용) ──
        private void PasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarning.Visibility =
                (Console.CapsLock && txtPassword.IsKeyboardFocusWithin)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // ── 포커스 떠날 때 경고 숨김 ──
        private void PasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarning.Visibility = Visibility.Collapsed;
        }

        // ── 패스워드 박스 포커스 진입 시:
        //    Admin 권한 ID 리스트와 현재 입력된 ID를 비교하여
        //    Admin Mode 체크박스 표시 여부 결정 ──
        private void PasswordBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
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
                Console.WriteLine(ex.Message + " PasswordBox_GotFocus function Check");
            }

            
        }
    }
}