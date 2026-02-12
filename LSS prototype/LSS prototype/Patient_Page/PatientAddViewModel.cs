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

        private void Save()//환자 정보 추가에 관한 입력, 검증만 담당
        {
            if (PatientCode == null || PatientCode == 0)
            {
                new CustomMessageWindow("환자 코드를 입력해주세요.", CustomMessageWindow.MessageBoxType.Ok).Show();
                return;
            }

            if (string.IsNullOrWhiteSpace(PatientName))
            {
                new CustomMessageWindow("환자 이름을 입력해주세요.").Show();
                return;
            }

            if (BirthDate == null)
            {
                new CustomMessageWindow("생년월일을 선택해주세요.").Show();
                return;
            }

            if (string.IsNullOrWhiteSpace(Sex))
            {
                new CustomMessageWindow("성별을 선택해주세요.").Show();
                return;
            }

            // ✅ 여기서는 저장하지 말고 그냥 닫기만
            CloseAction?.Invoke(true);
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
