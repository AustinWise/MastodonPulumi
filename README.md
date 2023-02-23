# One-button deployment of Mastodon on Google Cloud Platform using Pulumi

Well, that's the goal. This is a bit of work in progress. Currently it's hard-coded
to deploy to my domain.

Manual steps to complete:

1. In GCP Console:
   1. Create a project
   2. Create a Cloud DNS zone for your domain. Setup your registrar to
      [delegate DNS resolution to Google Cloud](https://cloud.google.com/dns/docs/update-name-servers).
2. Create a send-grid account and set it up with your domain above

Once you have setup your project, clone this repo and
configure you Pulumi stack to use your GCP project:

```bash
git clone https://github.com/AustinWise/MastodonPulumi.git
cd MastodonPulumi
pulumi config set gcp:project "project-id-goes-here"
```

Then run these commands to generate the Vapid public/private keypair:

```bash
dotnet run gen-vapid
```

Run the commands printed by the above command.

Then deploy with:

```bash
pulumi up
```

This will create a Kubernetes cluster from scratch on GKE and deploy the Mastodon
Helm chart. This includes setting up the right DNS records and
[provisioning a TLS certificate](https://cloud.google.com/kubernetes-engine/docs/how-to/managed-certs).

## TODO

* Make all settings configurable.
* See if there are a way to setup email with less effort. Ideally using something
  built into GCP. There is also a [Mailgun](https://www.pulumi.com/registry/packages/mailgun/)
  package for Pulumi.
* Consider hosting the Postgres outside of the Kubernettes cluster using Google Cloud SQL. This could take care of the HA and backups and whatnot.
* See if there is a nicer way to create the Values for the Helm chart.
* Make sure the way we are generating the Vapid key pair is actually compatible
  with Vapid and is not somehow insecure. See [Vapid's code](https://github.com/ClearlyClaire/webpush/blob/master/lib/webpush/vapid_key.rb#L33-L57).
* See if there is a way to reduce the cost of running this.
* Figure out why the GKE cluster seems to be in a "repairing" state often after running `pulumi up`
* Publish chart in a permanent location
  * Or if the upstream chart is published, figure out how to make an overlay of some sort.
* Figure out correct value for `openssl_verify_mode`.
* Consider switching to the native provider for GCP.
