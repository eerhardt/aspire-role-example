[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Azure.Identity")]

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
namespace RoleExample.Web
{

    public class WeatherApiClient(HttpClient httpClient)
    {
        public async Task<int> GetCurrentCountAsync(CancellationToken cancellationToken = default)
        {
            var countString = await httpClient.GetStringAsync("/getcount", cancellationToken);
            return int.Parse(countString);
        }
    }
}