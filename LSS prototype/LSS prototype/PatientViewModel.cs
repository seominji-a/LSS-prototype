using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace LSS_prototype
{
    public class PatientViewModel
    {
        public ICommand SyncClickCommand { get; }

        public PatientViewModel()
        {
            SyncClickCommand = new RelayCommand(SyncButtonClicked);
        }

        private void SyncButtonClicked()
        {
            MessageBox.Show("당일 예약된 EMR 환자 정보가 최신 상태로 업데이트되었습니다 .");
        }

    }
}
