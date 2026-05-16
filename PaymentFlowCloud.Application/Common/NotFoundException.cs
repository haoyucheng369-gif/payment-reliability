namespace PaymentFlowCloud.Application.Common;

public class NotFoundException(string resourceName, object resourceId) : Exception(
    $"{resourceName} '{resourceId}' was not found.")
{
    public string ResourceName { get; } = resourceName;

    public object ResourceId { get; } = resourceId;
}
