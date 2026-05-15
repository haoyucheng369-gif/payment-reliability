using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Payments;

public class CreatePaymentService(
    IPaymentRepository paymentRepository,
    IPaymentEventPublisher paymentEventPublisher)
{
    public async Task<Payment> CreateAsync(
        CreatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        // 创建支付只负责落库和发布领域事件，后续幂等校验会放在这里扩展。
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantOrderId = command.MerchantOrderId,
            Amount = command.Amount,
            Currency = command.Currency,
            TraceId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };

        await paymentRepository.AddAsync(payment, cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        // 先保存数据库，再发布消息，保证消费者能按 PaymentId 查到记录。
        await paymentEventPublisher.PublishPaymentCreatedAsync(payment, cancellationToken);

        return payment;
    }
}
