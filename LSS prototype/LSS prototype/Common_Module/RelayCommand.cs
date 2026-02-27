using System;
using System.Windows.Input;

namespace LSS_prototype
{
    internal class RelayCommand : ICommand
    {

        private readonly Action _execute;
        private readonly Func<bool> _canExecute;


        private readonly Action<object> _executeParam;
        private readonly Predicate<object> _canExecuteParam;


        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

   
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _executeParam = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecuteParam = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_executeParam != null)
                return _canExecuteParam == null || _canExecuteParam(parameter);

            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            if (_executeParam != null)
                _executeParam(parameter);
            else
                _execute();
        }


        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
