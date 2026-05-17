using Microsoft.Extensions.Logging;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Common;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Payments;

public class CreatePaymentService(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    IPaymentEventPublisher paymentEventPublisher,
    ILogger<CreatePaymentService> logger)
{
    public async Task<Payment> CreateAsync(
        CreatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.FindByIdAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogWarning("Payment creation rejected because order {OrderId} was not found", command.OrderId);
            throw new NotFoundException("Order", command.OrderId);
        }

        // OrderId 是当前支付创建的幂等键，同一个订单重复点击 Pay 直接返回已有 Payment。
        var existingPayment = await paymentRepository.FindByOrderIdAsync(
            command.OrderId,
            cancellationToken);

        if (existingPayment is not null)
        {
            logger.LogInformation(
                "Payment idempotency hit for order {OrderId}, returning payment {PaymentId}",
                command.OrderId,
                existingPayment.Id);

            return existingPayment;
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            MerchantOrderId = order.MerchantOrderId,
            Amount = order.Amount,
            Currency = order.Currency,
            CorrelationId = command.CorrelationId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await paymentRepository.AddAsync(payment, cancellationToken);
            await paymentRepository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Payment {PaymentId} created for order {OrderId} and merchant order {MerchantOrderId}",
                payment.Id,
                payment.OrderId,
                payment.MerchantOrderId);
        }
        catch (DuplicateOrderPaymentException ex)
        {
            // 并发重复点击可能同时通过前置查询，唯一索引失败后再查一次已有 Payment。
            var concurrentPayment = await paymentRepository.FindByOrderIdAsync(
                ex.OrderId,
                cancellationToken);

            if (concurrentPayment is not null)
            {
                logger.LogInformation(
                    "Concurrent payment idempotency hit for order {OrderId}, returning payment {PaymentId}",
                    ex.OrderId,
                    concurrentPayment.Id);

                return concurrentPayment;
            }

            throw;
        }

        // 先保存数据库，再发布消息，保证消费者能按 PaymentId 查到记录。
        await paymentEventPublisher.PublishPaymentCreatedAsync(payment, cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} creation event published for order {OrderId}",
            payment.Id,
            payment.OrderId);

        return payment;
    }
}
