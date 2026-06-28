using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Application.Abstractions;

                                                                public interface IPaymentProviderClient
{
    Task SubmitPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
}
