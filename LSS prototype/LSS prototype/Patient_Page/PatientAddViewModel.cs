using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype
{
    public class PatientAddViewModel
    {
        public string PatientName { get; set; }
        public int? PatientCode { get; set; }

        public DateTime? BirthDate { get; set; }

        public string Sex { get; set; }


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
            // 1. 환자 코드 검사 (null 또는 0 체크)
            if (PatientCode == null || PatientCode == 0)
            {
                MessageBox.Show("환자 코드를 입력해주세요.");
                return;
            }

            // 2. 이름 검사
            if (string.IsNullOrWhiteSpace(PatientName))
            {
                MessageBox.Show("환자 이름을 입력해주세요.");
                return;
            }

            // 3. 생년월일 검사
            if (BirthDate == null)
            {
                MessageBox.Show("생년월일을 선택해주세요.");
                return;
            }

            // 4. 성별 검사
            if (string.IsNullOrWhiteSpace(Sex))
            {
                MessageBox.Show("성별을 선택해주세요.");
                return;
            }

            var repo = new DB_Manager();
            bool result = repo.AddPatient(this);

            if (result)
            {
                MessageBox.Show("환자가 정상적으로 등록되었습니다.");
                CloseAction?.Invoke(true);
            }
            else
            {
                MessageBox.Show("등록 중 오류가 발생했습니다.");
                CloseAction?.Invoke(false);
            }
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
