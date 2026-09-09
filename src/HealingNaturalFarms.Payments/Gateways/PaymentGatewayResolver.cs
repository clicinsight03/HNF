using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Payments.Abstractions;
using HealingNaturalFarms.Payments.Options;

namespace HealingNaturalFarms.Payments.Gateways;

public class PaymentGatewayResolver : IPaymentGatewayResolver
{
    private readonly Dictionary<PaymentProvider, IPaymentGateway> _gateways;
    private readonly PaymentsOptions _options;

    public PaymentGatewayResolver(IEnumerable<IPaymentGateway> gateways, PaymentsOptions options)
    {
        _gateways = gateways.ToDictionary(g => g.Provider);
        _options = options;
    }

    public IPaymentGateway Resolve(PaymentProvider provider)
    {
        if (!_gateways.TryGetValue(provider, out var gateway))
        {
            throw new NotSupportedException($"No payment gateway registered for provider {provider}.");
        }

        return gateway;
    }

    public IReadOnlyList<PaymentProvider> GetAvailableProviders(RegionCode region) =>
        _options.RegionProviders.TryGetValue(region, out var providers) ? providers : [];
}
