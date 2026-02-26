using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{

    /// <summary>
    /// INotifyPropertyChanged : UI에게 알릴 준비가 됐다는 I/F
    /// IDisposable : 자원정리 I/F
    /// </summary>
    public class UserViewModel : INotifyPropertyChanged, IDisposable
    {
        public ICommand AddUserCommand { get; }
        public ICommand SettingCommand { get; }
        public ICommand DefaultCommand { get; }

        private readonly SearchDebouncer _searchDebouncer;
        private readonly IDialogService _dialogService;

        private string _searchText;

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
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();

                // 입력 바뀔 때마다 디바운서에 전달 → 0.5초 후 DB 검색
                _searchDebouncer.OnTextChanged(value);
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
            _searchDebouncer = new SearchDebouncer(ExecuteSearch, delayMs: 500);

            LoadUsers();
        }

        public void OnSearchTextChanged(string text)
        {
            _searchDebouncer.OnTextChanged(text);
        }

        private void ExecuteSearch(string keyword)
        {
            try
            {
                var repo = new DB_Manager();
                List<UserModel> data = string.IsNullOrWhiteSpace(keyword)
                    ? repo.GetAllUsers()
                    : repo.SearchUsers(keyword);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // 현재 선택된 유저 ID 기억
                    int? selectedId = SelectedUser?.UserId;

                    Users.Clear();
                    foreach (var user in data)
                        Users.Add(user);

                    // 같은 ID 가진 항목 다시 선택
                    if (selectedId.HasValue)
                        SelectedUser = Users.FirstOrDefault(u => u.UserId == selectedId.Value);
                });
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        public void Dispose()
        {
            _searchDebouncer?.Dispose();
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
                Common.WriteLog(ex);
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