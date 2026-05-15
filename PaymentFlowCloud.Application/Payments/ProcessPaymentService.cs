using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Payments;

public class ProcessPaymentService(IPaymentRepository paymentRepository)
{
    public async Task<bool> MarkProcessedAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        // Worker 只传 PaymentId，真正的状态流转规则由领域对象负责。
        var payment = await paymentRepository.FindByIdAsync(paymentId, cancellationToken);

        if (payment is null)
        {
            return false;
        }

        // 通过领域方法触发状态机，避免外部直接写 Status。
        payment.MarkProcessed();
        await paymentRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
