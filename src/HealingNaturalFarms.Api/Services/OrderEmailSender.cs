using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using HealingNaturalFarms.Domain.Entities;
using HealingNaturalFarms.Domain.Enums;
using Microsoft.Extensions.Options;

namespace HealingNaturalFarms.Api.Services;

/// <summary>
/// SMTP settings for outbound order-confirmation emails, bound from the
/// "Email" section in appsettings. Works with any SMTP relay - Gmail's
/// SMTP, SendGrid/Mailgun/Postmark's SMTP relay endpoints, AWS SES SMTP,
/// or an internal mail server - so nobody's locked into a specific
/// provider's API/SDK.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Healing Natural Farms";
}

public interface IOrderEmailSender
{
    /// <summary>
    /// Emails the customer their invoice: order/invoice number, the line
    /// items and prices, and the totals. Called right after a payment is
    /// confirmed (see CheckoutService.ConfirmPaymentAsync) - never before,
    /// since an unpaid order isn't a real invoice yet.
    /// </summary>
    Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default);
}

/// <summary>
/// Sends the post-purchase invoice email over plain SMTP via the BCL's
/// System.Net.Mail (no NuGet package required - this sandbox can't
/// restore one to verify against, and SmtpClient, while considered
/// legacy by Microsoft's docs, still fully supports STARTTLS/SSL SMTP
/// and needs nothing beyond credentials to work with any provider
/// above). If you later add NuGet access and want a more actively
/// maintained client (retry policies, DKIM helpers, etc.), MailKit's
/// SmtpClient is a drop-in swap behind this same IOrderEmailSender
/// interface - nothing else in the app needs to change.
/// </summary>
public class OrderEmailSender(IOptions<EmailOptions> options) : IOrderEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(order.ContactEmail))
        {
            return; // nothing to send to - shouldn't happen (checkout requires an email), but never throw over it
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost) || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Email:SmtpHost / Email:FromEmail are not configured.");
        }

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.Username)
                ? null
                : new NetworkCredential(_options.Username, _options.Password)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = $"Your Healing Natural Farms order {order.OrderNumber} is confirmed",
            Body = BuildPlainTextBody(order),
            IsBodyHtml = false
        };
        message.To.Add(order.ContactEmail);

        // Multipart alternative: a plain-text body (set above, for clients
        // that prefer/require it) plus an HTML view most inboxes will
        // actually render - AlternateViews is exactly what this is for.
        var htmlView = AlternateView.CreateAlternateViewFromString(BuildHtmlBody(order), Encoding.UTF8, "text/html");
        message.AlternateViews.Add(htmlView);

        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, ct);
    }

    private static string BuildPlainTextBody(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Invoice / Order Number: {order.OrderNumber}");
        sb.AppendLine($"Date: {order.CreatedAt.ToLocalTime():f}");
        sb.AppendLine();
        sb.AppendLine("Items:");
        foreach (var item in order.Items)
        {
            sb.AppendLine($"  {item.ProductNameSnapshot} x{item.Quantity} @ {FormatMoney(item.UnitPrice, order.Currency)} = {FormatMoney(item.LineTotal, order.Currency)}");
        }
        sb.AppendLine();
        sb.AppendLine($"Subtotal: {FormatMoney(order.Subtotal, order.Currency)}");
        sb.AppendLine($"Shipping: {FormatMoney(order.ShippingCost, order.Currency)}");
        sb.AppendLine($"Tax:      {FormatMoney(order.Tax, order.Currency)}");
        sb.AppendLine($"Total:    {FormatMoney(order.Total, order.Currency)}");

        if (order.ShippingAddress is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Shipping to:");
            sb.AppendLine($"  {order.ShippingAddress.FullName}");
            sb.AppendLine($"  {order.ShippingAddress.Line1}");
            if (!string.IsNullOrWhiteSpace(order.ShippingAddress.Line2)) sb.AppendLine($"  {order.ShippingAddress.Line2}");
            sb.AppendLine($"  {order.ShippingAddress.City}, {order.ShippingAddress.State} {order.ShippingAddress.PostalCode}");
            sb.AppendLine($"  {order.ShippingAddress.Country}");
        }

        sb.AppendLine();
        sb.AppendLine("Thank you for shopping with Healing Natural Farms!");
        return sb.ToString();
    }

    private static string BuildHtmlBody(Order order)
    {
        var rows = new StringBuilder();
        foreach (var item in order.Items)
        {
            rows.Append($"""
                <tr>
                    <td style="padding:8px 0;border-bottom:1px solid #e6ddc8;">{WebUtility.HtmlEncode(item.ProductNameSnapshot)}</td>
                    <td style="padding:8px 0;border-bottom:1px solid #e6ddc8;text-align:center;">{item.Quantity}</td>
                    <td style="padding:8px 0;border-bottom:1px solid #e6ddc8;text-align:right;">{FormatMoney(item.UnitPrice, order.Currency)}</td>
                    <td style="padding:8px 0;border-bottom:1px solid #e6ddc8;text-align:right;">{FormatMoney(item.LineTotal, order.Currency)}</td>
                </tr>
                """);
        }

        var addressHtml = order.ShippingAddress is null
            ? ""
            : $"""
               <p style="margin:4px 0;color:#22281f;">
                   {WebUtility.HtmlEncode(order.ShippingAddress.FullName)}<br/>
                   {WebUtility.HtmlEncode(order.ShippingAddress.Line1)}<br/>
                   {(string.IsNullOrWhiteSpace(order.ShippingAddress.Line2) ? "" : WebUtility.HtmlEncode(order.ShippingAddress.Line2) + "<br/>")}
                   {WebUtility.HtmlEncode(order.ShippingAddress.City)}, {WebUtility.HtmlEncode(order.ShippingAddress.State)} {WebUtility.HtmlEncode(order.ShippingAddress.PostalCode)}<br/>
                   {WebUtility.HtmlEncode(order.ShippingAddress.Country)}
               </p>
               """;

        return $"""
            <div style="font-family:'Segoe UI',Helvetica,Arial,sans-serif;background:#faf7f0;padding:24px;color:#22281f;">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:10px;overflow:hidden;">
                <div style="background:#1f3d2b;color:#ffffff;padding:20px 24px;">
                  <h1 style="margin:0;font-size:20px;">Healing Natural Farms</h1>
                  <p style="margin:4px 0 0;color:#dce9df;">Order confirmed</p>
                </div>
                <div style="padding:24px;">
                  <p style="margin:0 0 4px;"><strong>Invoice / Order Number:</strong> {WebUtility.HtmlEncode(order.OrderNumber)}</p>
                  <p style="margin:0 0 20px;color:#6b6f63;">{order.CreatedAt.ToLocalTime():f}</p>

                  <table style="width:100%;border-collapse:collapse;font-size:14px;">
                    <thead>
                      <tr style="text-align:left;color:#6b6f63;font-size:12px;text-transform:uppercase;">
                        <th style="padding-bottom:8px;">Item</th>
                        <th style="padding-bottom:8px;text-align:center;">Qty</th>
                        <th style="padding-bottom:8px;text-align:right;">Price</th>
                        <th style="padding-bottom:8px;text-align:right;">Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows}
                    </tbody>
                  </table>

                  <div style="margin-top:16px;font-size:14px;">
                    <div style="display:flex;justify-content:space-between;padding:2px 0;"><span>Subtotal</span><span>{FormatMoney(order.Subtotal, order.Currency)}</span></div>
                    <div style="display:flex;justify-content:space-between;padding:2px 0;"><span>Shipping</span><span>{FormatMoney(order.ShippingCost, order.Currency)}</span></div>
                    <div style="display:flex;justify-content:space-between;padding:2px 0;"><span>Tax</span><span>{FormatMoney(order.Tax, order.Currency)}</span></div>
                    <div style="display:flex;justify-content:space-between;padding:6px 0;margin-top:6px;border-top:1px solid #e6ddc8;font-weight:700;color:#2f5d3a;"><span>Total</span><span>{FormatMoney(order.Total, order.Currency)}</span></div>
                  </div>

                  <div style="margin-top:20px;padding-top:16px;border-top:1px solid #e6ddc8;">
                    <h3 style="margin:0 0 6px;color:#2f5d3a;font-size:14px;">Shipping to</h3>
                    {addressHtml}
                  </div>

                  <p style="margin-top:24px;color:#6b6f63;font-size:13px;">Thank you for shopping with Healing Natural Farms!</p>
                </div>
              </div>
            </div>
            """;
    }

    private static string FormatMoney(decimal amount, CurrencyCode currency) =>
        currency == CurrencyCode.USD
            ? amount.ToString("C", CultureInfo.GetCultureInfo("en-US"))
            : amount.ToString("C", CultureInfo.GetCultureInfo("en-IN"));
}
