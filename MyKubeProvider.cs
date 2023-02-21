using Pulumi;
using Pulumi.Gcp.Compute;
using Pulumi.Gcp.Container;
using Pulumi.Gcp.Container.Inputs;
using Pulumi.Gcp.Container.Outputs;

namespace MastodonPulumi
{
    class MyKubeProvider
    {
        public MyKubeProvider(Settings settings)
        {
            var network = new Network("mastodon-network", new NetworkArgs()
            {
            });

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
                },
                ReleaseChannel = new ClusterReleaseChannelArgs()
                {
                    Channel = "REGULAR",
                },
                BinaryAuthorization = new ClusterBinaryAuthorizationArgs()
                {
                    EvaluationMode = "DISABLED",
                },
            });

            var kubeConfig = Output.Tuple(cluster.Name, cluster.Endpoint, cluster.MasterAuth).Apply(t => GetKubeconfig(t.Item1, t.Item2, t.Item3));

            this.KubeProvider = new Pulumi.Kubernetes.Provider("mastodon-cluster-kube",
                new Pulumi.Kubernetes.ProviderArgs()
                {
                    KubeConfig = kubeConfig,
                },
                new CustomResourceOptions()
                {
                    DependsOn = cluster,
                }
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
