using Pulumi;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MastodonPulumi
{
    internal class Settings
    {
        public static async Task<Settings> LoadSettingsAsync(Config config)
        {
            var clientIpSetting = config.Get("ClientIp");
            IPAddress clientIp;
            if (clientIpSetting is null)
            {
                clientIp = await GetMyIpAddressAsync();
            }
            else
            {
                clientIp = IPAddress.Parse(clientIpSetting);
            }
            return new Settings(config, clientIp);
        }

        // TODO: load settings
        public Settings(Config config, IPAddress myIp)
        {
            // TODO: figure out how we should select the zone and region
            Region = Output.Create("us-central1");
            Zone = Output.Create("us-central1-a");

            DnsZoneName = Output.Create("happy-turtle-zone");
            DomainName = Output.Create("happy-turtle.dev");
            SmtpPassword = config.RequireSecret("SmtpPassword");
            this.VapidKey = new VapidKey(config);
            MyIp = myIp;
        }

        public Output<string> Region { get; }
        public Output<string> Zone { get; }
        public Output<string> DnsZoneName { get; }
        public Output<string> DomainName { get; }
        public Output<string> SmtpPassword { get; }
        public IPAddress MyIp { get; }

        public VapidKey VapidKey { get; }

        static async Task<IPAddress> GetMyIpAddressAsync()
        {
            using var httpClient = new HttpClient();
            string myIpStr = await httpClient.GetStringAsync("https://api.ipify.org");
            return IPAddress.Parse(myIpStr);
        }
    }
}
