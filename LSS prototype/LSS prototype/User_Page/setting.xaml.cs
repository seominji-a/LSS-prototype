using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public partial class setting : Window
    {
        // IP 마스크 상수 (공백 5개 기준)
        // 마스크: "     .     .     .     " (23자)
        // 인덱스: 0~4=옥텟1, 5='.', 6~10=옥텟2, 11='.', 12~16=옥텟3, 17='.', 18~22=옥텟4
        private const int OCTET_SIZE = 5;
        private readonly int[] _octetStarts = { 0, 6, 12, 18 };

        public setting()
        {
            InitializeComponent();
            DataContext = new SettingViewModel();
            Loaded += Window_Loaded;
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ResetIpBox(CStoreIPTextBox);
            ResetIpBox(MwlIPTextBox);

            // DB에서 기존 IP 불러올 경우 양식에 맞춰 바인딩
            var vm = DataContext as SettingViewModel;
            SetIpToBox(CStoreIPTextBox, vm.CStoreIP);
            SetIpToBox(MwlIPTextBox, vm.MwlIP);
        }


        private void ResetIpBox(TextBox tb)
        {
            tb.Text = "     .     .     .     ";
            tb.CaretIndex = 0;
        }

        private void SetIpToBox(TextBox tb, string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return;
            var parts = ip.Split('.');
            if (parts.Length != 4) return;

            var centered = parts.Select(p =>
            {
                int totalPad = OCTET_SIZE - p.Length;
                int padLeft = totalPad / 2;
                int padRight = totalPad - padLeft;
                return new string(' ', padLeft) + p + new string(' ', padRight);
            });

            tb.Text = string.Join(".", centered);
        }

        public string GetIpFromBox(TextBox tb)
        {
            var parts = tb.Text.Split('.');
            return string.Join(".", parts.Select(p => p.Trim()));
        }

        private int GetOctetStart(int caretIndex)
            => _octetStarts.LastOrDefault(s => s <= caretIndex);



        // 포커스 진입 시 커서 맨 좌측
        private void IPTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            tb.Dispatcher.BeginInvoke(new Action(() => tb.CaretIndex = 0));
        }

        // 숫자만 허용 + 중앙 배치 + . 자동 스킵
        private void IPTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;

            if (!char.IsDigit(e.Text[0])) return;

            var tb = sender as TextBox;
            if (tb == null) return;

            int caret = tb.CaretIndex;
            int octetStart = GetOctetStart(caret);

            string octetRaw = tb.Text.Substring(octetStart, OCTET_SIZE);
            string digits = octetRaw.Replace(" ", "");

            if (digits.Length >= 3) return;

            digits += e.Text[0];

            int totalPad = OCTET_SIZE - digits.Length;
            int padLeft = totalPad / 2;
            int padRight = totalPad - padLeft;
            string newOctet = new string(' ', padLeft) + digits + new string(' ', padRight);

            var chars = tb.Text.ToCharArray();
            for (int i = 0; i < OCTET_SIZE; i++)
                chars[octetStart + i] = newOctet[i];

            tb.Text = new string(chars);
            tb.CaretIndex = octetStart + padLeft + digits.Length;

            // ViewModel에 IP 값 동기화
            SyncIpToViewModel(tb);

            if (digits.Length >= 3)
                MoveToNextOctet(tb);
        }

        // 백스페이스 + 엔터
        private void IPTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                MoveToNextOctet(tb);
                return;
            }

            if (e.Key != Key.Back) return;
            e.Handled = true;

            int caret = tb.CaretIndex;
            int octetStart = GetOctetStart(caret);

            string octetRaw = tb.Text.Substring(octetStart, OCTET_SIZE);
            string digits = octetRaw.Replace(" ", "");

            if (digits.Length == 0)
            {
                int prevStart = _octetStarts.LastOrDefault(s => s < octetStart);
                if (prevStart < octetStart)
                    tb.CaretIndex = prevStart;
                return;
            }

            digits = digits.Substring(0, digits.Length - 1);

            string newOctet;
            if (digits.Length == 0)
            {
                newOctet = new string(' ', OCTET_SIZE);
            }
            else
            {
                int totalPad = OCTET_SIZE - digits.Length;
                int padLeft = totalPad / 2;
                int padRight = totalPad - padLeft;
                newOctet = new string(' ', padLeft) + digits + new string(' ', padRight);
            }

            var chars = tb.Text.ToCharArray();
            for (int i = 0; i < OCTET_SIZE; i++)
                chars[octetStart + i] = newOctet[i];

            tb.Text = new string(chars);

            int newPadLeft = (OCTET_SIZE - digits.Length) / 2;
            tb.CaretIndex = octetStart + newPadLeft + digits.Length;

            // ViewModel에 IP 값 동기화
            SyncIpToViewModel(tb);
        }

        // 커서가 . 위에 멈추지 않게 보정
        private void IPTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            if (tb.SelectionLength > 0) return;

            int caret = tb.CaretIndex;
            if (caret < tb.Text.Length && tb.Text[caret] == '.')
                tb.CaretIndex = caret + 1;
        }

        // 다음 옥텟으로 커서 이동
        private void MoveToNextOctet(TextBox tb)
        {
            int caret = tb.CaretIndex;
            int octetStart = GetOctetStart(caret);
            int nextStart = _octetStarts.FirstOrDefault(s => s > octetStart);

            if (nextStart == 0) return;
            tb.CaretIndex = nextStart;
        }

        // IP 입력값을 ViewModel 프로퍼티에 동기화
        private void SyncIpToViewModel(TextBox tb)
        {
            var vm = DataContext as SettingViewModel;
            if (vm == null) return;

            string ip = GetIpFromBox(tb);

            if (tb.Name == nameof(CStoreIPTextBox))
                vm.CStoreIP = ip;
            else if (tb.Name == nameof(MwlIPTextBox))
                vm.MwlIP = ip;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
