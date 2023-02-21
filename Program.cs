using MastodonPulumi;
using Pulumi;
using Pulumi.Gcp.Compute;
using Pulumi.Gcp.Compute.Inputs;
using Pulumi.Gcp.ServiceAccount;
using Pulumi.Gcp.Storage;
using Pulumi.Gcp.Storage.Inputs;
using Pulumi.Kubernetes.Helm;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Random;
using System.Collections.Generic;

if (args.Length != 0 && args[0] == "gen-vapid")
{
    (string pubKey, string privKey) = VapidKey.GenKeys();
    System.Console.WriteLine("Run these commands to save the secret keys:");
    System.Console.WriteLine($"  pulumi config set --secret {VapidKey.PUB_SECRET_NAME} \"{pubKey}\"");
    System.Console.WriteLine($"  pulumi config set --secret {VapidKey.PRIV_SECRET_NAME} \"{privKey}\"");
    return 1;
}

return await Deployment.RunAsync(async () =>
{
    var cfg = new Config();
    var settings = await Settings.LoadSettingsAsync(cfg);

    // file bucket

    var serviceAccount = new Account("bucket-access-account", new AccountArgs()
    {
        AccountId = "bucket-access",
        Description = "Service account for accessing the storage bucket.",
    });

    var bucket = new Bucket("mastodon-files", new BucketArgs
    {
        Location = settings.Region.Apply(r => r.ToUpperInvariant()),
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

    var bucketAcl = new BucketACL("access-acls", new BucketACLArgs()
    {
        Bucket = bucket.Id,
        RoleEntities = new InputList<string>()
        {
            serviceAccount.Email.Apply(email => $"OWNER:user-{email}"),
        },
    });

    var bucketKeys = new HmacKey("bucket-keys", new HmacKeyArgs()
    {
        ServiceAccountEmail = serviceAccount.Email,
    });

    // kubernettes cluster

    var publicIp = new GlobalAddress("public-ip", new GlobalAddressArgs()
    {
        Description = "IP address for the ingress to the Kubernettes cluster",
    });

    var dnsRecord = new Pulumi.Gcp.Dns.RecordSet("public-domain-name", new Pulumi.Gcp.Dns.RecordSetArgs()
    {
        Name = settings.DomainName.Apply(n => n + "."),
        ManagedZone = settings.DnsZoneName,
        Ttl = 60,
        Type = "A",
        Rrdatas = publicIp.Address,
    });

    var managedCert = new ManagedSslCertificate("managed-cert", new ManagedSslCertificateArgs()
    {
        Description = "Certificate for public Mastodon web services.",
        Managed = new ManagedSslCertificateManagedArgs()
        {
            Domains = new InputList<string>()
            {
                settings.DomainName,
            },
        },
    });

    var myKube = new MyKubeProvider(settings);

    // helm chart values
    var secretKeyBase = new RandomPassword("secret-key-base", new RandomPasswordArgs()
    {
        Length = 128,
    });
    var otpSecret = new RandomPassword("otp-secret", new RandomPasswordArgs()
    {
        Length = 128,
    });
    var postgresPassword = new RandomPassword("postgres-password", new RandomPasswordArgs()
    {
        Length = 48,
    });
    var redisPassword = new RandomPassword("redis-password", new RandomPasswordArgs()
    {
        Length = 48,
    });

    var chartValues = HelmChart.CreateValues(
        settings.DomainName, settings.Region,
        bucket.Id, bucketKeys.AccessId, bucketKeys.Secret,
        secretKeyBase.Result, otpSecret.Result, settings.VapidKey.PublicKey, settings.VapidKey.PrivateKey,
        publicIp.Name, managedCert.Name,
        postgresPassword.Result, redisPassword.Result,
        settings.SmtpPassword);

    var chart = new Chart("mastodon-chart", new LocalChartArgs()
    {
        Path = "./chart",
        Values = chartValues,
    }, new ComponentResourceOptions()
    {
        Provider = myKube.KubeProvider,
    });


    // Export the DNS name of the bucket
    return new Dictionary<string, object?>
    {
        ["bucketName"] = bucket.Url,
        ["globalIp"] = publicIp.Address,
    };
});
