using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public partial class IpAddressBox : UserControl
    {
        public static readonly DependencyProperty IpAddressProperty =
            DependencyProperty.Register(
                nameof(IpAddress), typeof(string), typeof(IpAddressBox),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIpAddressPropertyChanged));

        public string IpAddress
        {
            get => (string)GetValue(IpAddressProperty);
            set => SetValue(IpAddressProperty, value);
        }

        private bool _updating;
        private TextBox[] _octets;

        public IpAddressBox()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                _octets = new[] { Oct1, Oct2, Oct3, Oct4 };
                UpdateOctetsFromIP(IpAddress);
            };
        }

        private static void OnIpAddressPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (IpAddressBox)d;
            if (!ctrl._updating)
                ctrl.UpdateOctetsFromIP(e.NewValue as string);
        }

        private void UpdateOctetsFromIP(string ip)
        {
            if (_octets == null) return;
            var parts = (ip ?? "").Split('.');
            for (int i = 0; i < 4; i++)
                _octets[i].Text = parts.Length == 4 ? parts[i] : string.Empty;
        }

        private void UpdateIPFromOctets()
        {
            if (_octets == null) return;
            _updating = true;
            IpAddress = string.Join(".", new[] { Oct1.Text, Oct2.Text, Oct3.Text, Oct4.Text });
            _updating = false;
        }

        private void Octet_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text[0]);
        }

        private void Octet_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = (TextBox)sender;
            UpdateIPFromOctets();

            if (tb.Text.Length == 3)
                MoveFocusForward(tb);
            else if (tb.Text.Length == 0)
                MoveFocusBackward(tb);
        }

        private void Octet_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var tb = (TextBox)sender;

            switch (e.Key)
            {
                case Key.Back:
                    // 이미 비어있을 때 backspace → 이전 옥텟 (삑 소리 방지)
                    if (tb.Text.Length == 0)
                    {
                        e.Handled = true;
                        MoveFocusBackward(tb);
                    }
                    break;

                case Key.OemPeriod:
                case Key.Decimal:
                case Key.Space:
                case Key.Enter:
                    e.Handled = true;
                    MoveFocusForward(tb);
                    break;
            }
        }

        // 다음 옥텟: SelectAll (덮어쓰기 가능하도록)
        private void MoveFocusForward(TextBox current)
        {
            int next = Array.IndexOf(_octets, current) + 1;
            if (next < 4)
            {
                _octets[next].Focus();
                _octets[next].SelectAll();
            }
        }

        // 이전 옥텟: 커서를 끝에 배치 (하나씩 삭제 가능하도록)
        private void MoveFocusBackward(TextBox current)
        {
            int prev = Array.IndexOf(_octets, current) - 1;
            if (prev >= 0)
            {
                _octets[prev].Focus();
                _octets[prev].CaretIndex = _octets[prev].Text.Length;
            }
        }

        // 빈 공간 클릭 시: 왼쪽 절반 → 첫 옥텟, 오른쪽 절반 → 마지막 옥텟
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_octets == null || e.Source is TextBox) return;

            double clickX = e.GetPosition(this).X;
            if (clickX < ActualWidth / 2.0)
            {
                Oct1.Focus();
                Oct1.CaretIndex = 0;
            }
            else
            {
                Oct4.Focus();
                Oct4.CaretIndex = Oct4.Text.Length;
            }
        }
    }
}
