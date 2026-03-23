using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LSS_prototype.Patient_Page
{
    public partial class PatientAddDialog : Window
    {
        public PatientAddDialog()
        {
            InitializeComponent();
            // ✅ 세션 모니터 등록 → 팝업 위에서 마우스 움직여도 세션 연장
            Loaded += (s, e) => App.ActivityMonitor?.RegisterWindow(this);
        }
    }
}