using Pulumi;
using Pulumi.Gcp.Projects;
using System.Collections.Generic;

namespace MastodonPulumi
{
    static class ServiceEnablement
    {
        public static CustomResourceOptions EnableRequiredServices()
        {
            // TODO: figure out how to enable these APIs without disabling them on `pulumi down`
            /*
            var dnsService = new Service("dns-service-enablement", new ServiceArgs()
            {
                ServiceName = "dns.googleapis.com",
            });
            var computeService = new Service("compute-service-enablement", new ServiceArgs()
            {
                ServiceName = "compute.googleapis.com",
            });
            var kubeService = new Service("kubernetes-service-enablement", new ServiceArgs()
            {
                ServiceName = "container.googleapis.com",
            });

            var ret = new CustomResourceOptions()
            {
                DependsOn = new List<Resource>()
                {
                    dnsService,
                    computeService,
                    kubeService,
                },
            };

            return ret;
            */
            return new CustomResourceOptions();
        }
    }
}
