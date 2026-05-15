using Stateless;

namespace PaymentFlowCloud.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }

    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    public string TraceId { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public void MarkProcessed()
    {
        Fire(PaymentTrigger.Process);
    }

    public void MarkFailed()
    {
        Fire(PaymentTrigger.Fail);
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
        var stateMachine = new StateMachine<PaymentStatus, PaymentTrigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(PaymentStatus.Pending)
            .Permit(PaymentTrigger.Process, PaymentStatus.Processed)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed);

        stateMachine.Configure(PaymentStatus.Processed)
            .Ignore(PaymentTrigger.Process);

        stateMachine.Configure(PaymentStatus.Failed)
            .Ignore(PaymentTrigger.Fail);

        return stateMachine;
    }
}
