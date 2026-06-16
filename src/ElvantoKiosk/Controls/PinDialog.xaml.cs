using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ElvantoKiosk.Controls;

public partial class PinDialog : Window
{
    private readonly string _expectedPin;
    private readonly StringBuilder _enteredPin = new();

    private const int MaxPinLength = 12;

    public PinDialog(string expectedPin)
    {
        InitializeComponent();
        _expectedPin = expectedPin ?? string.Empty;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        PinDisplay.Text = _enteredPin.Length == 0
            ? "—"
            : new string('●', _enteredPin.Length);
    }

    private void AppendDigit(string digit)
    {
        if (_enteredPin.Length >= MaxPinLength)
            return;

        _enteredPin.Append(digit);
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateDisplay();
    }

    private void Digit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string digit })
            AppendDigit(digit);
    }

    private void Backspace_Click(object sender, RoutedEventArgs e)
    {
        if (_enteredPin.Length > 0)
        {
            _enteredPin.Length--;
            ErrorText.Visibility = Visibility.Collapsed;
            UpdateDisplay();
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _enteredPin.Clear();
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateDisplay();
    }

    private void Validate()
    {
        if (_enteredPin.ToString() == _expectedPin && !string.IsNullOrEmpty(_expectedPin))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ErrorText.Visibility = Visibility.Visible;
            _enteredPin.Clear();
            UpdateDisplay();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Validate();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
