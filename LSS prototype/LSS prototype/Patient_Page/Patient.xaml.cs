using LSS_prototype.User_Page;
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

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// Patient.xaml에 대한 상호 작용 논리
    /// 2026-02-09 서민지
    /// </summary>
    public partial class Patient : UserControl
    {
        public Patient()
        {
            InitializeComponent();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Instance.NavigateTo(new User());
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
           CheckBox cb = sender as CheckBox;

            // 사용자가 클릭해서 체크가 된 상태일 때만 화면 전환
            if (cb.IsChecked == false)
            {
                MainPage.Instance.NavigateTo(new EmrPatient());
            }
        }
    }
}
