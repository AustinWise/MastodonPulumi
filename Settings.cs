using Pulumi;

namespace MastodonPulumi
{
    internal class Settings
    {
        // TODO: load settings
        public Settings(Config config)
        {
            // TODO: figure out how we should select the zone and region
            Region = Output.Create("us-central1");
            Zone = Output.Create("us-central1-a");

            DnsZoneName = Output.Create("happy-turtle-zone");
            DomainName = Output.Create("happy-turtle.dev");
            this.VapidKey = new VapidKey(config);
        }

        public Output<string> Region { get; }
        public Output<string> Zone { get; }
        public Output<string> DnsZoneName { get; }
        public Output<string> DomainName { get; }

        public VapidKey VapidKey { get; }
    }
}
