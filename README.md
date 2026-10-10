# ImagineVR
hacklanta 2 submission

## Backend

The FastAPI backend is in [openrouter-api](openrouter-api/README.md).
GitHub Actions tests it and deploys backend changes on `main` to the Vultr VM.
See the backend README for the required GitHub secret and deployment activation steps.

## Infrastructure

The Ubuntu server on Vultr is managed by Terraform in [infra/vultr](infra/vultr/README.md).
That directory contains the VM, firewall, SSH public key, first-boot configuration,
and import declarations for the existing server, plus connection and team setup instructions.
The VM's Python service and nginx setup are recorded in `infra/deploy/`.
