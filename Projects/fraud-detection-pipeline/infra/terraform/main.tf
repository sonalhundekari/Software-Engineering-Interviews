# Minimal AKS cluster provisioning stub for the demo environment.
# Intentionally small (2-node, B-series) since this is spun up on demand for
# demos, not run continuously. Run `terraform apply` before a demo,
# `terraform destroy` after.

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "fraud_demo" {
  name     = "rg-fraud-detection-demo"
  location = "West US 2"
}

resource "azurerm_kubernetes_cluster" "fraud_demo" {
  name                = "aks-fraud-detection-demo"
  location            = azurerm_resource_group.fraud_demo.location
  resource_group_name = azurerm_resource_group.fraud_demo.name
  dns_prefix          = "frauddemo"

  default_node_pool {
    name       = "default"
    node_count = 2
    vm_size    = "Standard_B2s"
  }

  identity {
    type = "SystemAssigned"
  }

  tags = {
    purpose = "portfolio-demo"
    ttl     = "ephemeral"
  }
}

output "kube_config" {
  value     = azurerm_kubernetes_cluster.fraud_demo.kube_config_raw
  sensitive = true
}
