output "instance_id" {
  value = vultr_instance.ubuntu.id
}

output "public_ip" {
  value = vultr_instance.ubuntu.main_ip
}

output "ssh_command" {
  value = "ssh -i ~/.ssh/hacklanta_vultr ubuntu@${vultr_instance.ubuntu.main_ip}"
}
