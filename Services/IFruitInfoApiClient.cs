using Object_Detection_ASP.NETMVC.Models.Api;

namespace Object_Detection_ASP.NETMVC.Services
{
    public interface IFruitInfoApiClient
    {
        Task<FruitInfoResponseDto> GetFruitAsync(string fruitName, CancellationToken cancellationToken = default);
    }
}
