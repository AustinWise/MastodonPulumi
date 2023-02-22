using Pulumi;
using System.Collections.Generic;

namespace MastodonPulumi
{
    static class HelmChart
    {
        public static InputMap<object> CreateValues(
            Output<string> domainName, Output<string> region,
            Output<string> bucketId, Output<string> bucketAccessId, Output<string> bucketSecret,
            Output<string> secretKeyBase, Output<string> otpSecret, Output<string> vapidPublicKey, Output<string> vapidPrivateKey,
            Output<string> publicIp, Output<string> managedCertId,
            Output<string> postgresPassword, Output<string> redisPassword,
            Output<string> smtpPassword)
        {
            Output<Dictionary<string, object>> ret = Output.Create(new Dictionary<string, object>()
            {
                { "image", new { tag = "v4.1.0" }
                },
                { "elasticsearch", new Dictionary<string, object>()
                    {
                        { "enabled", false }
                    }
                },
                { "mastodon", new Dictionary<string, object>() },
            });

            ret = Output.Tuple(ret, domainName, region, bucketId, bucketAccessId, bucketSecret).Apply(AddMastodonSettings);
            ret = Output.Tuple(ret, secretKeyBase, otpSecret, vapidPublicKey, vapidPrivateKey).Apply(AddMastodonSecret);
            ret = Output.Tuple(ret, smtpPassword).Apply(AddMastodonSmtp);
            ret = Output.Tuple(ret, domainName, publicIp, managedCertId).Apply(AddIngress);
            ret = Output.Tuple(ret, postgresPassword, redisPassword).Apply(AddDatabase);

            return ret;
        }

        private static Dictionary<string, object> AddMastodonSettings(
            (Dictionary<string, object>, string, string, string, string, string) tup)
        {
            (Dictionary<string, object> dic,
            string domainName, string region,
            string bucketId, string bucketAccessId, string bucketSecret) = tup;
            var masto = (Dictionary<string, object>)dic["mastodon"];
            masto["local_domain"] = domainName;
            masto["s3"] = new Dictionary<string, object>()
            {
                { "enabled", true },
                { "access_key", bucketAccessId },
                { "access_secret", bucketSecret },
                { "bucket", bucketId },
                { "endpoint", "https://storage.googleapis.com" },
                { "hostname", "storage.googleapis.com" },
                { "region", region },
            };
            return dic;
        }

        private static Dictionary<string, object> AddMastodonSecret((Dictionary<string, object>, string, string, string, string) tup)
        {
            (Dictionary<string, object> dic, string secretKeyBase, string otpSecret, string vapidPublicKey, string vapidPrivateKey) = tup;
            var masto = (Dictionary<string, object>)dic["mastodon"];
            masto["secrets"] = new Dictionary<string, object>()
            {
                { "secret_key_base", secretKeyBase },
                { "otp_secret", otpSecret },
                { "vapid", new Dictionary<string, object>()
                    {
                        { "public_key", vapidPublicKey },
                        { "private_key", vapidPrivateKey },
                    }
                },
            };
            return dic;
        }

        private static Dictionary<string, object> AddMastodonSmtp((Dictionary<string, object> dic, string password) tup)
        {
            (Dictionary<string, object> dic, string password) = tup;
            var masto = (Dictionary<string, object>)dic["mastodon"];
            //TODO: settings for more of these
            masto["smtp"] = new Dictionary<string, object>()
            {
                { "from_address", "notifications-test@kame.moe" },
                { "server", "smtp.sendgrid.net" },
                { "tls", true },
                { "login", "apikey" },
                { "password", password },
            };
            return dic;
        }

        private static Dictionary<string, object> AddIngress((Dictionary<string, object>, string, string, string) tup)
        {
            (Dictionary<string, object> dic, string domainName, string publicIp, string managedCertId) = tup;
            dic["ingress"] = new Dictionary<string, object>()
            {
                { "tls", null! },
                { "annotations", new Dictionary<string, object>()
                    {
                        { "kubernetes.io/ingress.class", "gke" },
                        { "kubernetes.io/ingress.global-static-ip-name", publicIp },
                        { "networking.gke.io/managed-certificates", managedCertId },
                    }
                },
                { "hosts", new List<Dictionary<string, object>>()
                    {
                        new Dictionary<string, object>()
                        {
                            { "host", domainName },
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
            };
            return dic;
        }

        private static Dictionary<string, object> AddDatabase((Dictionary<string, object>, string, string) tup)
        {
            (Dictionary<string, object> dic, string postgresPassword, string redisPassword) = tup;
            dic["postgresql"] = new Dictionary<string, object>()
            {
                { "auth", new Dictionary<string, object>()
                    {
                        { "password", postgresPassword },
                    }
                },
            };
            dic["redis"] = new Dictionary<string, object>()
            {
                // This makes a node, rather than a multi-node cluster
                { "architecture", "standalone" },
                { "password", redisPassword },
            };
            return dic;
        }
    }
}
