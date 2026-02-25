using LSS_prototype.DB_CRUD;
using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    public class PatientEditViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // 속성 변경 알림 헬퍼 메서드
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string PatientName { get; set; }
        public int? PatientCode { get; set; }

        public int Patient_id { get; set; }

        public DateTime? BirthDate { get; set; }

        public string Sex { get; set; }

        public ICommand EditCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<bool?> CloseAction { get; set; }
        public PatientEditViewModel(PatientModel selected)
        {
            Patient_id = selected.PatientId;
            PatientCode = selected.PatientCode;
            PatientName = selected.PatientName;
            BirthDate = selected.BirthDate;
            Sex = selected.Sex;

            EditCommand = new RelayCommand(UpdatePatient);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void UpdatePatient()
        {
            try
            {
                var repo = new DB_Manager();

                var model = new PatientModel
                {
                    PatientId = this.Patient_id,    
                    PatientCode = this.PatientCode.Value,
                    PatientName = this.PatientName,
                    BirthDate = this.BirthDate.Value,
                    Sex = this.Sex
                };

                if (repo.UpdatePatient(model))
                {
                    CustomMessageWindow.Show("수정되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    CloseAction?.Invoke(true);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
