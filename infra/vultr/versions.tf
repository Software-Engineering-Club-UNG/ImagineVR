terraform {
  required_version = ">= 1.5, < 2.0"

  required_providers {
    vultr = {
      source  = "vultr/vultr"
      version = "~> 2.32.0"
    }
  }
}

# Authentication comes from VULTR_API_KEY, never from a committed file.
provider "vultr" {}
