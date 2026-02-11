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

namespace LSS_prototype
{
    public class PatientEditViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // 3. 속성 변경 알림 헬퍼 메서드
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string PatientName { get; set; }
        public int? PatientCode { get; set; }

        public DateTime? BirthDate { get; set; }

        public char Sex { get; set; }

        public ICommand EditCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<bool?> CloseAction { get; set; }
        public PatientEditViewModel(PatientModel selected)
        {
            PatientCode = selected.PatientCode;
            PatientName = selected.Name;
            BirthDate = selected.BRITH_DATE;
            Sex = selected.Sex;
            EditCommand = new RelayCommand(UpdatePatient);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void UpdatePatient()
        {
            var repo = new DB_Manager();
            // DB_Manager의 업데이트 메서드 호출
            if (repo.UpdatePatient(this))
            {
                MessageBox.Show("수정되었습니다.");
                CloseAction?.Invoke(true); // 성공 결과와 함께 창 닫기
            }
        
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
