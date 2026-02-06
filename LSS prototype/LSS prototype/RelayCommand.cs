using System;
using System.Windows.Input;

namespace LSS_prototype
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute; // 실행할 로직
        private readonly Predicate<object> _canExecute; // 버튼 활성화 여부

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // 버튼이 눌러도 되는 상태인지 확인 (일단 항상 true)
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        // 버튼을 눌렀을 때 실행되는 실제 로직
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            // CommandManager가 버튼의 활성화/비활성 상태를 자동으로 체크하도록 연결합니다.
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}