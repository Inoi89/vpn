namespace VpnClient.Core.Interfaces;

public interface IWintunService
{
    Task CreateAdapterAsync(string name);
    Task DeleteAdapterAsync(string name);
}
