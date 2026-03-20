using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models.Auth;
using VpnClient.Infrastructure.Auth;

namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel
{
    private readonly IProductPlatformAuthService _productPlatformAuthService;
    private readonly ProductPlatformOptions _productPlatformOptions;

    [ObservableProperty]
    private ProductPlatformSession? accountSession;

    [ObservableProperty]
    private bool isAccountScreenOpen;

    [ObservableProperty]
    private bool isAuthenticating;

    [ObservableProperty]
    private string authEmail = string.Empty;

    [ObservableProperty]
    private string authPassword = string.Empty;

    [ObservableProperty]
    private string authErrorMessage = string.Empty;

    public bool IsAuthenticated => AccountSession is not null;

    public bool ShowOnboardingScreen => HasNoProfiles && !IsAuthenticated && !IsAccountScreenOpen;

    public bool ShowAccountScreen => IsAccountScreenOpen || (HasNoProfiles && IsAuthenticated);

    public bool CanDismissAccountScreen => HasProfiles || ShowOnboardingScreen;

    public bool ShowAuthForm => !IsAuthenticated;

    public bool ShowAuthenticatedCard => IsAuthenticated;

    public bool ShowAccountShortcut => HasProfiles || IsAuthenticated;

    public bool CanSignIn =>
        !IsBusy
        && !IsAuthenticating
        && !string.IsNullOrWhiteSpace(AuthEmail)
        && !string.IsNullOrWhiteSpace(AuthPassword);

    public string AccountTitle => IsAuthenticated ? "Аккаунт" : "Вход";

    public string AccountSubtitle => IsAuthenticated
        ? "Управление аккаунтом и выдачей доступов."
        : "Войдите в аккаунт или зарегистрируйтесь через личный кабинет.";

    public string AccountButtonBadge => IsAuthenticated
        ? AccountSession!.DisplayName[..1].ToUpperInvariant()
        : string.Empty;

    public string AccountIdentityText => IsAuthenticated
        ? $"{AccountSession!.DisplayName}\n{AccountSession.Email}"
        : "Войдите, чтобы подключать управляемые доступы и работать с кабинетом.";

    public string LegacyImportHintText => HasProfiles
        ? "Старые импортированные конфиги продолжают работать как раньше."
        : "Если у вас уже есть .vpn или .conf, его можно импортировать без авторизации.";

    public string AccountPrimaryActionText => IsAuthenticated ? "Открыть кабинет" : "Войти";

    public async Task RestoreAccountSessionAsync()
    {
        AccountSession = await _productPlatformAuthService.GetCurrentSessionAsync();
        if (AccountSession is not null)
        {
            AuthEmail = AccountSession.Email;
            AuthPassword = string.Empty;
        }

        NotifyViewStateChanged();
    }

    [RelayCommand]
    private void OpenAccountScreen()
    {
        AuthErrorMessage = string.Empty;
        IsAccountScreenOpen = true;
        NotifyViewStateChanged();
    }

    [RelayCommand]
    private void CloseAccountScreen()
    {
        if (!CanDismissAccountScreen)
        {
            return;
        }

        AuthErrorMessage = string.Empty;
        IsAccountScreenOpen = false;
        NotifyViewStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        try
        {
            IsAuthenticating = true;
            AuthErrorMessage = string.Empty;
            NotifyViewStateChanged();

            AccountSession = await _productPlatformAuthService.LoginAsync(AuthEmail, AuthPassword);
            AuthEmail = AccountSession.Email;
            AuthPassword = string.Empty;
            AuthErrorMessage = string.Empty;

            if (HasProfiles)
            {
                IsAccountScreenOpen = false;
            }

            LastOperationMessage = $"Аккаунт '{AccountSession.DisplayName}' подключен.";
        }
        catch (Exception exception)
        {
            AuthErrorMessage = exception.Message;
            LastOperationMessage = $"Не удалось войти: {exception.Message}";
            _logger.LogWarning(exception, "Product platform login failed.");
        }
        finally
        {
            IsAuthenticating = false;
            NotifyViewStateChanged();
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        try
        {
            IsAuthenticating = true;
            NotifyViewStateChanged();
            await _productPlatformAuthService.LogoutAsync();
            AccountSession = null;
            AuthPassword = string.Empty;
            AuthErrorMessage = string.Empty;
            LastOperationMessage = "Аккаунт отключен.";
        }
        catch (Exception exception)
        {
            AuthErrorMessage = exception.Message;
            LastOperationMessage = $"Не удалось выйти: {exception.Message}";
            _logger.LogWarning(exception, "Product platform logout failed.");
        }
        finally
        {
            IsAuthenticating = false;
            NotifyViewStateChanged();
        }
    }

    [RelayCommand]
    private void OpenRegistrationPortal()
    {
        OpenExternalUrl(_productPlatformOptions.CabinetUrl);
    }

    [RelayCommand]
    private void OpenCabinetPortal()
    {
        OpenExternalUrl(_productPlatformOptions.CabinetUrl);
    }

    partial void OnAuthEmailChanged(string value)
    {
        SignInCommand.NotifyCanExecuteChanged();
    }

    partial void OnAuthPasswordChanged(string value)
    {
        SignInCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAuthenticatingChanged(bool value)
    {
        SignInCommand.NotifyCanExecuteChanged();
    }

    private static void OpenExternalUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
