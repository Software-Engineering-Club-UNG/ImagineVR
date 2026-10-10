# Vultr Ubuntu server

This directory manages the existing ImagineVR/Hacklanta server with Terraform.

| Setting | Value |
| --- | --- |
| Instance | `hacklanta-ubuntu` |
| Instance ID | `5fcb37a2-82ba-4c98-b24c-4869c9c99eb1` |
| Public IPv4 | `64.177.52.162` |
| Region | Atlanta (`atl`) |
| OS | Ubuntu Server 26.04 LTS x64 (`2760`) |
| Plan | `vc2-1c-1gb`: 1 vCPU, 1 GB RAM, 25 GB SSD |
| Base price at deployment | $5/month, plus applicable taxes/usage charges |
| Inbound firewall | TCP 22 (SSH), TCP 80 (HTTP) |
| Login | `ubuntu`, SSH key only, with sudo |

`main.tf` defines the VM, SSH public key, firewall group, and SSH rule.
`cloud-init.yaml.tftpl` defines first-boot configuration. `imports.tf` identifies
the existing resources so a fresh checkout adopts them instead of creating duplicates.

## Using Terraform

Install Terraform 1.5 or newer and use a Vultr account with access to these resources.
Set `VULTR_API_KEY` in your environment; never put it in this repo.
On this Windows machine the key is already saved as a user environment variable.
In an existing PowerShell window, load it with:

```powershell
$env:VULTR_API_KEY = [Environment]::GetEnvironmentVariable('VULTR_API_KEY', 'User')
terraform -chdir=infra/vultr init
terraform -chdir=infra/vultr plan
```

Review the plan before running `terraform -chdir=infra/vultr apply`.
On a fresh checkout the plan should import the five existing resources, with no
new VM. The existing VM has `prevent_destroy` enabled to block accidental replacement.
Changes to installation-time SSH key IDs and cloud-init are ignored because they
are absent from imported state and the provider would otherwise replace the VM.
The cloud-init template documents bootstrap behavior and configures future new
deployments; configure existing users over SSH when adding team access.

Terraform state and saved plans are ignored by Git because they can contain the
server's generated password. The provider lock file should be committed.
State currently lives locally: coordinate with the other operator before applying
changes. A shared remote backend should be configured before concurrent team use.
After configuring shared state, migrate this local state with `terraform init -migrate-state`.

## Connecting

On the Windows machine that created the server:

```powershell
ssh -i "$env:USERPROFILE\.ssh\hacklanta_vultr" ubuntu@64.177.52.162
```

Only the public key is in Git. The private key is local at
`C:\Users\parke\.ssh\hacklanta_vultr`. A teammate needs their own public key
added to `/home/ubuntu/.ssh/authorized_keys` by an existing operator before they
can connect. Do not share private keys or API credentials in the repo.

Provider reference: https://registry.terraform.io/providers/vultr/vultr/latest/docs
