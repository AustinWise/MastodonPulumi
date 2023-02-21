# One-button deployment of Mastodon on Google Cloud Platform using Pulumi

Well, that's the goal. This is a bit of work in progress. Currently it's hard-coded
to deploy to my domain.

Manual steps to complete:

1. In GCP Console:
   1. Create a project
   2. Register a domain name, or at least delegate the zone to GCP
2. Create a send-grid account and set it up with your domain above

Once you have setup your project, clone this repo and
configure you Pulumi stack to use your GCP project:

```bash
git clone --recurse-submodules https://github.com/AustinWise/MastodonPulumi.git
cd MastodonPulumi
pulumi config set gcp:project "project-name-goes-here"
```

Then run these commands to generate the Vapid public/private keypair:

```bash
dotnet run gen-vapid
```

Run the commands printed by the above command.

Then restore the Helm chart depandcies. You will need the [Helm](https://helm.sh/)
command installed:

```bash
pushd chart
helm dep update
popd
```

Then deploy with:

```bash
pulumi up
```

This will create a Kubernettes cluster from scratch on GKE and deploy the Mastodon
Helm chart. This includes setting up the right DNS records and
[provisioning a TLS certificate](https://cloud.google.com/kubernetes-engine/docs/how-to/managed-certs).

## TODO

* Make all settings configurable.
* See if there are a way to setup email with less effort. Ideally using something
  built into GCP. There is also a [Mailgun](https://www.pulumi.com/registry/packages/mailgun/)
  package for Pulumi.
* Consider hosting the Postgres outside of the Kubernettes cluster using Google Cloud SQL. This could take care of the HA and backups and whatnot.
* Switch to using the Helm Release resource, for supporting hooks on Mastodon version upgrade.
  See [this blog post](https://www.pulumi.com/blog/full-access-to-helm-features-through-new-helm-release-resource-for-kubernetes/).
* See if there is a nicer way to create the Values for the Helm chart.
* Make sure the way we are generating the Vapid key pair is actually compatible
  with Vapid and is not somehow insecure. See [Vapid's code](https://github.com/ClearlyClaire/webpush/blob/master/lib/webpush/vapid_key.rb#L33-L57).
* See if there is a way to reduce the cost of running this.
