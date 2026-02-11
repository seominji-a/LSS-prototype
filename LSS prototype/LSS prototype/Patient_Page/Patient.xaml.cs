using LSS_prototype.Login_Page;
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

namespace LSS_prototype
{
    /// <summary>
    /// Patient.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Patient : Window
    {

        public string PatientId { get; set; }
        public int PatientCode { get; set; }
        public string Name { get; set; }

        public DateTime BRITH_DATE { get; set; }

        public char Sex { get; set; }

        public DateTime Reg_Date { get; set; }
        public Patient()
        {
            InitializeComponent();
            this.DataContext = new PatientListViewModel();

            /*if (!AuthToken.EnsureAuthenticated())
            {
                Console.WriteLine("로그인이 필요합니다.");

                new Login().Show();
                this.Close();
                return;
            }*/
            // MainWindow는 인증된 세션에서만 접근해야 함. ( 현재 프로그램 내 화면간 이동, 모달창 호출은 세션 및 토큰기반으로 관리 )
            // 생성자에서 EnsureAuthenticated() 체크를 하면, 다른 코드에서 MainWindow를 강제로 new 해서 Show()하는 우회 경로를 차단할 수 있음.
            // 현재는 UI/플로우 테스트 중이라 임시로 비활성화(주석)했으며, 기능 안정화 후 반드시 다시 활성화할 것. (2026-02-11, 박한용)
            


        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {

            var win = new User();
            win.Show();
            this.Close(); // 필요하면
        }
    }
}
