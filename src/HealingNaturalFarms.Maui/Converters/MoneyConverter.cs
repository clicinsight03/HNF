using System.Globalization;
using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Maui.Converters;

/// <summary>
/// Formats a price using the storefront's own currency (USD/INR) rather
/// than the device's current locale - a US phone browsing the India
/// storefront should still see rupees. Bind the whole DTO (`{Binding .}`
/// or just `{Binding}`) rather than the bare decimal, since ProductListItemDto
/// and ProductDetailDto each carry their own Currency alongside Price.
///
/// CartItemDto/order line items don't carry a Currency of their own
/// (only the parent CartDto/OrderDetailDto does) - those are formatted
/// in code-behind instead (see CartPage/OrderConfirmationPage), since a
/// single-value converter can't reach a sibling property on the parent
/// without a MultiBinding, and a hand-built display string is simpler
/// and more obviously correct than wiring one up here.
/// </summary>
public class MoneyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (amount, currency) = value switch
        {
            ProductListItemDto p => (p.Price, (CurrencyCode?)p.Currency),
            ProductDetailDto p => (p.Price, (CurrencyCode?)p.Currency),
            _ => ((decimal?)null, (CurrencyCode?)null)
        };

        if (amount is null || currency is null) return string.Empty;
        return Format(amount.Value, currency.Value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static string Format(decimal amount, CurrencyCode currency) =>
        currency == CurrencyCode.USD
            ? amount.ToString("C", CultureInfo.GetCultureInfo("en-US"))
            : amount.ToString("C", CultureInfo.GetCultureInfo("en-IN"));
}
