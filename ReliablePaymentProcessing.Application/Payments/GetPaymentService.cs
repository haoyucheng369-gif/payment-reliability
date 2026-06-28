using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Application.Payments;

public class GetPaymentService(IPaymentRepository paymentRepository)
{
    public async Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
                                                          return await paymentRepository.FindByIdAsync(id, cancellationToken);
    }
}
