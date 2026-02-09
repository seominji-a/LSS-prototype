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
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new User();
            win.Show();
            this.Close(); // 필요하면
        }
    }
}
