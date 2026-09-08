terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "~> 4.0" }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

resource "azurerm_resource_group" "shared" {
  name     = var.resource_group_name
  location = var.location
}

# Storage account for audit (each app would have its own in production)
resource "azurerm_storage_account" "shared" {
  name                     = var.storage_account_name
  resource_group_name      = azurerm_resource_group.shared.name
  location                 = azurerm_resource_group.shared.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
}

# App Service plan (F1 free for demo)
resource "azurerm_service_plan" "api" {
  name                = "asp-shared-library"
  resource_group_name = azurerm_resource_group.shared.name
  location            = azurerm_resource_group.shared.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
}

# Assessment Portal app (bundles Mandrill library directly)
resource "azurerm_linux_web_app" "assessment" {
  name                = var.web_app_name
  resource_group_name = azurerm_resource_group.shared.name
  location            = azurerm_resource_group.shared.location
  service_plan_id     = azurerm_service_plan.api.id
  https_only          = true

  site_config {
    application_stack { dotnet_version = "10.0" }
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT = "Demo"
    # NOTE: In production, Mandrill API key would be here
    # In the shared library approach, THIS KEY IS DUPLICATED IN EVERY APP
  }
}
