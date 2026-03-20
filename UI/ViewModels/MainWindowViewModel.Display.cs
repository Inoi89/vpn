namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel
{
    public string ShellProfileLabel => SelectedProfile is null
        ? "Выберите сервер"
        : SelectedProfile.IsManagedProfile
            ? "Управляемый профиль"
            : "Импортированный профиль";

    public string ShellEndpointText => SelectedProfile?.Endpoint ?? ConnectionState.Endpoint ?? "Не указан";

    public string ShellEndpointHostText => ExtractHost(ShellEndpointText) ?? "Сервер не выбран";

    public string ServerPanelUpdateText => ShowUpdateAction
        ? UpdateActionText
        : "Проверить обновления";
}
