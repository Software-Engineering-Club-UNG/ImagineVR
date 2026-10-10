# Adopt the existing resources, including from a fresh checkout with no state.
# Keep these blocks while the team uses local state to avoid duplicate servers.
import {
  to = vultr_instance.ubuntu
  id = "5fcb37a2-82ba-4c98-b24c-4869c9c99eb1"
}

import {
  to = vultr_ssh_key.windows
  id = "ff89a246-1d50-4743-88d5-b29234e4ca3e"
}

import {
  to = vultr_firewall_group.server
  id = "4387ce36-152b-40e8-9fab-d1e6e54e989c"
}

import {
  to = vultr_firewall_rule.ssh
  id = "4387ce36-152b-40e8-9fab-d1e6e54e989c,1"
}

import {
  to = vultr_firewall_rule.http
  id = "4387ce36-152b-40e8-9fab-d1e6e54e989c,2"
}
