using LSS_prototype;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype
{
    internal class PatientListViewModel
    {
        private readonly IDialogService _dialogService;

        public ICommand PatientAddCommand { get; }

        public PatientListViewModel()
        {
            _dialogService = new Dialog();
            PatientAddCommand = new RelayCommand(AddPatient);
        }

        private void AddPatient()
        {
            var vm = new PatientAddViewModel();
            var result = _dialogService.ShowDialog(vm);
        }
    }
}
