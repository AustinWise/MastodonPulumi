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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

if (args.Length != 0 && args[0] == "gen-vapid")
{
    (string pubKey, string privKey) = VapidKey.GenKeys();
    System.Console.WriteLine("Run these commands to save the secret keys:");
    System.Console.WriteLine($"  pulumi config set --secret {VapidKey.PUB_SECRET_NAME} \"{pubKey}\"");
    System.Console.WriteLine($"  pulumi config set --secret {VapidKey.PRIV_SECRET_NAME} \"{privKey}\"");
    return 1;
}

return await Deployment.RunAsync(() =>
{
    var cfg = new Config();
    var settings = new Settings(cfg);

    //var firewall = new Firewall("mastodon-firewall", new FirewallArgs()
    //{
    //    Network = network.Id,
    //    Allows = new List<FirewallAllowArgs>()
    //    {
    //        new FirewallAllowArgs()
    //        {
    //            Ports = new List<string>()
    //            {
    //                "80",
    //                "443",
    //            },
    //            Protocol = "tcp",
    //        },
    //    },
    //    SourceRanges = new List<string>()
    //    {
    //        myIp.ToString(),
    //    },
    //});

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
    var vapidKey = new VapidKey(cfg);

    var chartValues = new Dictionary<string, object>()
    {
        { "image", new Dictionary<string, object>()
            {
                { "tag", "v4.1.0" },
            }
        },
        { "mastodon", new Dictionary<string, object>()
            {
                { "local_domain", settings.DomainName },
                { "s3", new Dictionary<string, object>()
                    {
                        { "enabled", true },
                        { "access_key", bucketKeys.AccessId },
                        { "access_secret", bucketKeys.Secret },
                        { "bucket", bucket.Id },
                        { "endpoint", "https://storage.googleapis.com" },
                        { "hostname", "storage.googleapis.com" },
                        { "region", settings.Region },
                    }
                },
                { "secrets", new Dictionary<string, object>()
                    {
                        { "secret_key_base", secretKeyBase.Result },
                        { "otp_secret", otpSecret.Result },
                        { "vapid", new Dictionary<string, object>()
                            {
                                { "private_key", vapidKey.PrivateKey },
                                { "public_key", vapidKey.PublicKey },
                            }
                        },
                    }
                },
            }
        },
        { "ingress", new Dictionary<string, object>()
            {
                { "tls", null! },
                { "annotations", new Dictionary<string, object>()
                    {
                        { "kubernetes.io/ingress.class", "gke" },
                        { "kubernetes.io/ingress.global-static-ip-name", publicIp.Address },
                        { "networking.gke.io/managed-certificates", managedCert.Id },
                    }
                },
                { "hosts", new List<Dictionary<string, object>>()
                    {
                        new Dictionary<string, object>()
                        {
                            { "host", settings.DomainName },
                            { "paths", new List<Dictionary<string, object>>()
                                {
                                    new Dictionary<string, object>()
                                    {
                                        { "path", "/" },
                                    },
                                }
                            },
                        },
                    }
                },
            }
        },
        { "elasticsearch", new Dictionary<string, object>()
            {
                { "enabled", false }
            }
        },
        { "postgresql", new Dictionary<string, object>()
            {
                { "auth", new Dictionary<string, object>()
                    {
                        { "password", postgresPassword },
                    }
                },
            }
        },
        { "redis", new Dictionary<string, object>()
            {
                // This makes a node, rather than a multi-node cluster
                { "architecture", "standalone" },
                { "password", redisPassword },
            }
        },
    };

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
