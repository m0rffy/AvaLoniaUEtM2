using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UETM2;

public static class Validation
{
    public static void NumericOnlyCheck_KeyPress(object sender, KeyEventArgs e)
    {
        char c = e.Key.ToString()[0]; // упрощённо, в Avalonia KeyPress работает иначе
        if (!char.IsControl(c) && !char.IsDigit(c) && c != '-' && c != '.')
            e.Handled = true;
    }

    public static void NumericOnlyCheck_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            string original = textBox.Text ?? "";
            string withDot = original.Replace(',', '.');
            string filtered = new string(withDot.Where(c => char.IsDigit(c) || c == '-' || c == '.').ToArray());
            if (original != filtered)
            {
                textBox.Text = filtered;
                textBox.CaretIndex = textBox.Text.Length;
            }
        }
    }

    public static void NumericOnly(TextBox textBox)
    {
        textBox.AddHandler(InputElement.KeyDownEvent, NumericOnlyCheck_KeyPress);
        textBox.TextChanged += NumericOnlyCheck_TextChanged;
    }
}
