namespace VpnClient.UI.ViewModels;

public enum UiNotificationLevel
{
    Information,
    Success,
    Warning,
    Error
}

public sealed record UiNotificationRequest(
    string Title,
    string Message,
    UiNotificationLevel Level);
