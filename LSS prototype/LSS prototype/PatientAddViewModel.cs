using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype
{
    public class PatientAddViewModel
    {
        public string LoginId { get; set; }
        public string Password { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<bool?> CloseAction { get; set; }
        public PatientAddViewModel()
        {
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save()
        {

            // TODO: DB 저장
            CloseAction?.Invoke(true);
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
