using System.Globalization;
using System.Windows.Controls;
using static DspNetEnergy.Desktop.UiLanguage;

namespace DspNetEnergy.Desktop;

internal sealed class InputError(TextBox field, string message) : ArgumentException(message)
{
    internal TextBox Field { get; } = field;
}

internal static class NumericInput
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    internal static decimal? Decimal(TextBox field, string label, bool optional = false, bool positive = false)
    {
        string value = field.Text.Trim();
        if (optional && value.Length == 0)
        {
            return null;
        }

        if (!TryDecimal(value, out decimal result) ||
            result < 0m || (positive && result == 0m))
        {
            string message = positive
                ? Format("{0} must be a positive number.", "{0} muss eine positive Zahl sein.", label)
                : Format("{0} must be a nonnegative number.", "{0} muss eine nichtnegative Zahl sein.", label);
            throw new InputError(field, message);
        }

        return result;
    }

    internal static int? Count(TextBox field, string label)
    {
        string value = field.Text.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result)
            ? result
            : throw new InputError(field, Format("{0} must be a nonnegative whole number.",
                "{0} muss eine nichtnegative ganze Zahl sein.", label));
    }

    internal static void Reformat(TextBox field)
    {
        if (TryDecimal(field.Text.Trim(), out decimal value))
        {
            field.Text = Ui.Edit(value);
        }
    }

    private static bool TryDecimal(string value, out decimal result)
    {
        const NumberStyles styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
        return decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out result) ||
            decimal.TryParse(value, styles, German, out result);
    }
}
