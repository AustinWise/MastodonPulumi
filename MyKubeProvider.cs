using Pulumi;
using Pulumi.Gcp.Compute;
using Pulumi.Gcp.Compute.Inputs;
using Pulumi.Gcp.Container;
using Pulumi.Gcp.Container.Inputs;
using Pulumi.Gcp.Container.Outputs;
using System.Collections.Generic;

namespace MastodonPulumi
{
    class MyKubeProvider
    {
        public MyKubeProvider(Settings settings, CustomResourceOptions requiredServices)
        {
            var network = new Network("mastodon-network", new NetworkArgs()
            {
            }, requiredServices);

            var firewall = new Firewall("mastodon-firewall", new FirewallArgs()
            {
                Network = network.Id,
                Allows = new List<FirewallAllowArgs>()
                {
                    new FirewallAllowArgs()
                    {
                        Ports = new List<string>()
                        {
                            "80",
                            "443",
                        },
                        Protocol = "tcp",
                    },
                    new FirewallAllowArgs()
                    {
                        Protocol = "icmp",
                    },
                },
                SourceRanges = new List<string>()
                {
                    "0.0.0.0/0",
                },
            }, requiredServices);

            var cluster = new Cluster("mastodon-cluster", new ClusterArgs()
            {
                EnableAutopilot = true,
                Location = settings.Region,
                NodeConfig = new ClusterNodeConfigArgs()
                {
                },
                Network = network.Id,
                IpAllocationPolicy = new ClusterIpAllocationPolicyArgs()
                {
                    ClusterIpv4CidrBlock = "/17",
                    ServicesIpv4CidrBlock = "/22",
                },
                VerticalPodAutoscaling = new ClusterVerticalPodAutoscalingArgs()
                {
                    Enabled = true,
                },
                MasterAuthorizedNetworksConfig = new ClusterMasterAuthorizedNetworksConfigArgs()
                {
                    // TODO: figure out if we really need to only allow our IP to access the control plane.
                    CidrBlocks = new ClusterMasterAuthorizedNetworksConfigCidrBlockArgs()
                    {
                        CidrBlock = $"{settings.MyIp}/32",
                        DisplayName = "My IP",
                    },
                    GcpPublicCidrsAccessEnabled = false,
                },
                ReleaseChannel = new ClusterReleaseChannelArgs()
                {
                    Channel = "REGULAR",
                },
                BinaryAuthorization = new ClusterBinaryAuthorizationArgs()
                {
                    EvaluationMode = "DISABLED",
                },
            }, requiredServices);

            var kubeConfig = Output.Tuple(cluster.Name, cluster.Endpoint, cluster.MasterAuth).Apply(t => GetKubeconfig(t.Item1, t.Item2, t.Item3));

            requiredServices = CustomResourceOptions.Merge(requiredServices, new CustomResourceOptions()
            {
                DependsOn = new InputList<Resource>()
                    {
                        cluster,
                        firewall,
                    },
            });

            this.KubeProvider = new Pulumi.Kubernetes.Provider("mastodon-cluster-kube",
                new Pulumi.Kubernetes.ProviderArgs()
                {
                    KubeConfig = kubeConfig,
                },
                requiredServices
            );
        }

        public Pulumi.Kubernetes.Provider KubeProvider { get; }
        static string GetKubeconfig(string clusterName, string clusterEndpoint, ClusterMasterAuth clusterMasterAuth)
        {
            var context = $"{Pulumi.Gcp.Config.Project}_{Pulumi.Gcp.Config.Zone}_{clusterName}";
            return $@"apiVersion: v1
clusters:
- cluster:
    certificate-authority-data: {clusterMasterAuth.ClusterCaCertificate}
    server: https://{clusterEndpoint}
  name: {context}
contexts:
- context:
    cluster: {context}
    user: {context}
  name: {context}
current-context: {context}
kind: Config
preferences: {{}}
users:
- name: {context}
  user:
    exec:
      apiVersion: client.authentication.k8s.io/v1beta1
      command: gke-gcloud-auth-plugin
      installHint: Install gke-gcloud-auth-plugin for use with kubectl by following
        https://cloud.google.com/blog/products/containers-kubernetes/kubectl-auth-changes-in-gke
      provideClusterInfo: true
";
        }
    }
}
