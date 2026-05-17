using Microsoft.Extensions.Logging;
using PaymentFlowCloud.Application.Abstractions;

namespace PaymentFlowCloud.Application.Payments;

public class ProcessPaymentService(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    ILogger<ProcessPaymentService> logger)
{
    public async Task<bool> MarkProcessedAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        // Worker 只传 PaymentId，真正的状态流转规则由领域对象负责。
        var payment = await paymentRepository.FindByIdAsync(paymentId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Payment {PaymentId} was not found during worker processing", paymentId);
            return false;
        }

        payment.MarkProcessed();

        if (payment.OrderId is not null)
        {
            var order = await orderRepository.FindByIdAsync(payment.OrderId.Value, cancellationToken);
            order?.MarkPaid();
        }

        await paymentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} processed and order {OrderId} marked paid",
            payment.Id,
            payment.OrderId);

        return true;
    }
}
