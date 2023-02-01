using Pulumi;
using Pulumi.Gcp.Compute;
using Pulumi.Gcp.Compute.Inputs;
using Pulumi.Gcp.Storage;
using Pulumi.Gcp.Storage.Inputs;
using System.Collections.Generic;
using System.Linq;

// TODO: figure out how we should select the zone and region
const string REGION = "us-central1";
const string ZONE = REGION + "-a";

const string DNS_ZONE_NAME = "happy-turtle-zone";
const string DNS_NAME = "happy-turtle.dev";

return await Deployment.RunAsync(() =>
{
    var network = new Network("mastodon-network");

    var firewall = new Firewall("mastodon-firewall", new FirewallArgs()
    {
        Network = network.Id,
        Allows = new List<FirewallAllowArgs>()
        {
            new FirewallAllowArgs()
            {
                Ports = new List<string>()
                {
                    "22"
                },
                Protocol = "tcp",
            },
        },
        SourceRanges = new List<string>()
        {
            // TODO: get current user's IP address
        },
    });


    var webServer = new Instance("web-server", new InstanceArgs()
    {
        BootDisk = new InstanceBootDiskArgs()
        {
            InitializeParams = new InstanceBootDiskInitializeParamsArgs()
            {
                Image = "debian-11",
                Type = "pd-balanced",
                Size = 20,
            },
        },
        EnableDisplay = false,
        NetworkInterfaces = new List<InstanceNetworkInterfaceArgs>()
        {
            new InstanceNetworkInterfaceArgs()
            {
                Network = network.Id,
                AccessConfigs = new List<InstanceNetworkInterfaceAccessConfigArgs>()
                {
                    new InstanceNetworkInterfaceAccessConfigArgs()
                    {
                        NetworkTier = "PREMIUM",
                        PublicPtrDomainName = DNS_NAME,
                    },
                },
                StackType = "IPV4_ONLY",
            },
        },
        MachineType = "e2-small",
        Zone = ZONE,
    });

    //Pulumi.Gcp.Sql.DatabaseInstance
    // Create a GCP resource (Storage Bucket)
    var bucket = new Bucket("mastodon-files", new BucketArgs
    {
        Location = REGION.ToUpperInvariant(),
        Cors = new List<BucketCorArgs>()
        {
            new BucketCorArgs()
            {
                Methods = "GET",
                Origins = "*",
            },
        },
        // Mastodon needs ACLs. It uses it to hide media posted by suspended accounts.
        UniformBucketLevelAccess = false,
    });

    var webServerDns = new Pulumi.Gcp.Dns.RecordSet(DNS_NAME, new Pulumi.Gcp.Dns.RecordSetArgs()
    {
        Name = DNS_NAME + ".",
        ManagedZone = DNS_ZONE_NAME,
        Ttl = 60,
        Type = "A",
        Rrdatas = webServer.NetworkInterfaces.First().Apply(i => i.AccessConfigs.First().NatIp),
    });

    // Export the DNS name of the bucket
    return new Dictionary<string, object?>
    {
        ["bucketName"] = bucket.Url
    };
});
