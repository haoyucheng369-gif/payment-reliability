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
        // MerchantOrderId 是业务幂等键，重复请求直接返回已有支付，不重复发布消息。
        var existingPayment = await paymentRepository.FindByMerchantOrderIdAsync(
            command.MerchantOrderId,
            cancellationToken);

        if (existingPayment is not null)
        {
            return existingPayment;
        }

        // 创建支付只负责落库和发布领域事件，后续更细粒度幂等键会在这里扩展。
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantOrderId = command.MerchantOrderId,
            Amount = command.Amount,
            Currency = command.Currency,
            CorrelationId = command.CorrelationId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await paymentRepository.AddAsync(payment, cancellationToken);
            await paymentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateMerchantOrderException)
        {
            // 并发请求可能同时通过前置查询，唯一索引失败后再查一次已有支付。
            var concurrentPayment = await paymentRepository.FindByMerchantOrderIdAsync(
                command.MerchantOrderId,
                cancellationToken);

            if (concurrentPayment is not null)
            {
                return concurrentPayment;
            }

            throw;
        }

        // 先保存数据库，再发布消息，保证消费者能按 PaymentId 查到记录。
        await paymentEventPublisher.PublishPaymentCreatedAsync(payment, cancellationToken);

        return payment;
    }
}
