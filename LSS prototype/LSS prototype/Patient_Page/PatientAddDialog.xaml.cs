using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LSS_prototype.Views
{
    /// <summary>
    /// PatientAddDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PatientAddDialog : UserControl
    {
        public PatientAddDialog()
        {
            InitializeComponent();
            DpBirthDate.BlackoutDates.Add(new CalendarDateRange(DateTime.Today.AddDays(1), DateTime.MaxValue));
        }
        private void DpBirthDate_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DatePicker datePicker)
            {
                datePicker.IsDropDownOpen = true;

                // 마우스 캡처를 해제하여 달력 내부의 날짜 클릭이 정상 작동하게 합니다.
                if (Mouse.Captured is DatePickerTextBox)
                {
                    Mouse.Capture(null);
                }
            }
        }
    }
}
