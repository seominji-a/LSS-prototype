using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype
{
    public class PatientEditViewModel
    {
        public string PatientName { get; set; }
        public int? PatientCode { get; set; }

        public DateTime? BirthDate { get; set; }

        public char Sex { get; set; }

        public ICommand EditCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<bool?> CloseAction { get; set; }
        public PatientEditViewModel()
        {
            EditCommand = new RelayCommand(Edit);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Edit()
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
