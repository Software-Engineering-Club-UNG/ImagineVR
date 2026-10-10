resource "vultr_ssh_key" "windows" {
  name    = "hacklanta-windows"
  ssh_key = trimspace(file("${path.module}/keys/hacklanta-windows.pub"))
}

resource "vultr_firewall_group" "server" {
  description = "hacklanta-ubuntu-ssh"
}

resource "vultr_firewall_rule" "ssh" {
  firewall_group_id = vultr_firewall_group.server.id
  protocol          = "tcp"
  ip_type           = "v4"
  subnet            = "0.0.0.0"
  subnet_size       = 0
  port              = "22"
  notes             = "SSH key access"
}

resource "vultr_firewall_rule" "http" {
  firewall_group_id = vultr_firewall_group.server.id
  protocol          = "tcp"
  ip_type           = "v4"
  subnet            = "0.0.0.0"
  subnet_size       = 0
  port              = "80"
  notes             = "FastAPI HTTP via nginx"
}

resource "vultr_firewall_rule" "https" {
  firewall_group_id = vultr_firewall_group.server.id
  protocol          = "tcp"
  ip_type           = "v4"
  subnet            = "0.0.0.0"
  subnet_size       = 0
  port              = "443"
  notes             = "ImagineVR HTTPS API"
}

resource "vultr_instance" "ubuntu" {
  region            = "atl"
  plan              = "vc2-4c-8gb"
  os_id             = 2760 # Ubuntu 26.04 LTS x64
  label             = "hacklanta-ubuntu"
  hostname          = "hacklanta-ubuntu"
  firewall_group_id = vultr_firewall_group.server.id
  ssh_key_ids       = [vultr_ssh_key.windows.id]
  user_data = templatefile("${path.module}/cloud-init.yaml.tftpl", {
    public_key = vultr_ssh_key.windows.ssh_key
  })
  backups = "disabled"

  depends_on = [vultr_firewall_rule.ssh]

  lifecycle {
    prevent_destroy = true
    # Installation-time fields are absent from imported state. Changing them
    # would replace this VM; manage existing users/configuration over SSH.
    ignore_changes = [ssh_key_ids, user_data]
  }
}
