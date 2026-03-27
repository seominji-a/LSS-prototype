using LSS_prototype.ImageReview_Page;
using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
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

namespace LSS_prototype.VideoReview_Page
{
    /// <summary>
    /// VideoReview.xaml에 대한 상호 작용 논리
    /// </summary>

    public partial class VideoReview : UserControl
    {
        private bool _navOpen = false;
        private PatientModel _selectedPatient;

        public VideoReview(PatientModel patient, string emrcheck, string studyid)
        {
            InitializeComponent();
            _selectedPatient = patient;
            DataContext = new VideoReviewViewModel(patient, emrcheck, studyid);
        }

        private void ToggleNav_Click(object sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources[_navOpen ? "NavOut" : "NavIn"]).Begin();
            _navOpen = !_navOpen;
            ToggleBtn.Content = _navOpen ? "❮" : "❯";
        }
    }
}
