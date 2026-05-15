using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Payments;

public class GetPaymentService(IPaymentRepository paymentRepository)
{
    public async Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // 查询用例先保持简单，后续需要读模型时再单独拆 DTO。
        return await paymentRepository.FindByIdAsync(id, cancellationToken);
    }
}
