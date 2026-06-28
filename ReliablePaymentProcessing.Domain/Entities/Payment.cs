using System.Text.Json.Serialization;
using Stateless;

namespace ReliablePaymentProcessing.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }

    public Guid? OrderId { get; set; }

    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    public string CorrelationId { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public Order? Order { get; set; }

    public void MarkProcessing()
    {
    }

    public void MarkSucceeded()
    {
    }

    public void MarkFailed()
    {
    }

    private void Fire(PaymentTrigger trigger)
    {
        var stateMachine = CreateStateMachine();

        if (!stateMachine.CanFire(trigger))
        {
            throw new InvalidOperationException(
                $"Payment status cannot transition from {Status} using trigger {trigger}.");
        }

        stateMachine.Fire(trigger);
    }

    private StateMachine<PaymentStatus, PaymentTrigger> CreateStateMachine()
    {
        // Build the state machine from the current status so EF-loaded entities transition correctly.
        var stateMachine = new StateMachine<PaymentStatus, PaymentTrigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(PaymentStatus.Pending)
            .Permit(PaymentTrigger.Process, PaymentStatus.Processing)
            .Permit(PaymentTrigger.Succeed, PaymentStatus.Succeeded)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed);

        stateMachine.Configure(PaymentStatus.Processing)
            .Ignore(PaymentTrigger.Process)
            .Permit(PaymentTrigger.Succeed, PaymentStatus.Succeeded)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed);

        stateMachine.Configure(PaymentStatus.Succeeded)
            .Ignore(PaymentTrigger.Process)
            .Ignore(PaymentTrigger.Succeed);

        stateMachine.Configure(PaymentStatus.Failed)
            .Ignore(PaymentTrigger.Fail);

        return stateMachine;
    }
}
