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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LSS_prototype.Scan_Page
{
    /// <summary>
    /// Scan.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Scan : UserControl
    {
        private bool _navOpen = false;
        private bool _settingOpen = false;

        public Scan() => InitializeComponent();

        // 네비 토글
        private void ToggleNav_Click(object sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources[_navOpen ? "NavOut" : "NavIn"]).Begin();
            _navOpen = !_navOpen;
            ToggleBtn.Content = _navOpen ? ">" : "<";
        }

        // 설정창 토글 (애니메이션 없이 즉시)
        private void ToggleSetting_Click(object sender, RoutedEventArgs e)
        {
            _settingOpen = !_settingOpen;
            SettingPanel.Visibility = _settingOpen
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ★ 초기화 버튼 → 커스텀 메시지창 호출
        private void ResetSetting_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("초기화되었습니다.");
        }

        private void PatientButton_Click(object sender, RoutedEventArgs e)
            => MainPage.Instance.NavigateTo(new Patient_Page.Patient());

    

        private void Exit_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();
    }
}
