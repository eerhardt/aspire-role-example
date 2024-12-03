namespace RoleExample.Web;

public class WeatherApiClient(HttpClient httpClient)
{
    public async Task<int> GetCurrentCountAsync(CancellationToken cancellationToken = default)
    {
        var countString = await httpClient.GetStringAsync("/getcount", cancellationToken);
        return int.Parse(countString);
    }
}