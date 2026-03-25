using LSS_prototype.Patient_Page;
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

        public Scan(PatientModel selectedPatient, string studyId = null)
        {
            InitializeComponent();       
            DataContext = new ScanViewModel(selectedPatient, studyId);
            Unloaded += (s, e) => (DataContext as ScanViewModel)?.Dispose();
            Loaded += async (s, e) =>
            {
                var vm = DataContext as ScanViewModel;
                await vm.InitializeAsync(); // 렌즈 초기화 완료 후 카메라 연결
            };
        }


        // Nav 패널 열기/닫기 토글 (Setting이 열려있으면 먼저 닫음)
        private void ToggleNav_Click(object sender, RoutedEventArgs e)
        {
            if (!_navOpen && _settingOpen)
                CloseSetting();

            RunNavAnimation(!_navOpen);
        }

        // Setting 패널 열기/닫기 토글 (Nav가 열려있으면 먼저 닫음)
        private void ToggleSetting_Click(object sender, RoutedEventArgs e)
        {
            _settingOpen = !_settingOpen;
            SettingPanel.Visibility = _settingOpen ? Visibility.Visible : Visibility.Collapsed;

            if (_settingOpen && _navOpen)
                RunNavAnimation(false);
        }

        // Nav 패널 슬라이드 애니메이션 실행 및 상태 업데이트
        private void RunNavAnimation(bool open)
        {
            ((Storyboard)Resources[open ? "NavIn" : "NavOut"]).Begin();
            _navOpen = open;
            SetToggleButton(open);
        }

        // Nav 열림 상태에 따라 토글 버튼 아이콘 변경 (❮ / ❯)
        private void SetToggleButton(bool navOpen)
        {
            ToggleBtn.Content = new TextBlock
            {
                Text = navOpen ? "❯" : "❮",
                Foreground = Brushes.White,
                FontSize = 40,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // Setting 패널 강제 닫기
        private void CloseSetting()
        {
            _settingOpen = false;
            SettingPanel.Visibility = Visibility.Collapsed;
        }
    }
}
