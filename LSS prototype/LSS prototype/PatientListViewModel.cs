using LSS_prototype;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype
{
    internal class PatientListViewModel
    {
        private readonly IDialogService _dialogService;

        public ICommand PatientAddCommand { get; }
        public ICommand SyncClickCommand { get; }

        public PatientListViewModel()
        {
            _dialogService = new Dialog();
            PatientAddCommand = new RelayCommand(AddPatient);
            SyncClickCommand = new RelayCommand(SyncButtonClicked);
        }

        private void AddPatient()
        {
            var vm = new PatientAddViewModel();
            var result = _dialogService.ShowDialog(vm);
        }

        private void SyncButtonClicked()
        {
            MessageBox.Show("당일 예약된 EMR 환자 정보가 최신 상태로 업데이트되었습니다 .");
        }
    }
}
