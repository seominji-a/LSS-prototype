using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
   

    public class UserViewModel : INotifyPropertyChanged
    {
        public ICommand AddUserCommand { get; }
        public ICommand SettingCommand { get; }
        public ICommand DefaultCommand { get; }
        private readonly IDialogService _dialogService;

        private ObservableCollection<UserModel> _users = new ObservableCollection<UserModel>();
        public ObservableCollection<UserModel> Users
        {
            get { return _users; }
            set
            {
                _users = value;
                OnPropertyChanged();
            }
        }

        private UserModel _selectedUser;
        public UserModel SelectedUser
        {
            get { return _selectedUser; }
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
            }
        }

        public UserViewModel()
        {
            _dialogService = new Dialog();
            AddUserCommand = new RelayCommand(ExecuteAddUser);
            SettingCommand = new RelayCommand(ExecuteOpenSetting);
            DefaultCommand = new RelayCommand(ExecuteOpenDefault);

            LoadUsers();
        }

        // DB 로드하는 함수 

        public void LoadUsers()
        {
            try
            {
                var repo = new DB_Manager();
                List<UserModel> data = repo.GetAllUsers();
                Users = new ObservableCollection<UserModel>(data);
            }
            catch (Exception ex)
            {
                CustomMessageWindow.Show("데이터 로드 중 오류 발생: " + ex.Message,
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Danger);
            }
        }

        private void ExecuteAddUser(object parameter)
        {
            var vm = new User_AddViewModel();
            var result = _dialogService.ShowDialog(vm);

            if (result == true)
                LoadUsers();
        }
        private void ExecuteOpenSetting(object parameter)  
        {
            _dialogService.ShowSetting();
        }

        private void ExecuteOpenDefault(object parameter)
        {
            _dialogService.ShowDefault();
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}