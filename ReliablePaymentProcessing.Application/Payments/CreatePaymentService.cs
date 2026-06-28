using Microsoft.Extensions.Logging;
using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Application.Common;
using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Application.Payments;

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

                                                                    await paymentEventPublisher.PublishPaymentCreatedAsync(payment, cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} creation event published for order {OrderId}",
            payment.Id,
            payment.OrderId);

        return payment;
    }
}
