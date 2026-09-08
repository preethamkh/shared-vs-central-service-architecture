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

resource "azurerm_resource_group" "central" {
  name     = var.resource_group_name
  location = var.location
}

# Storage account for audit archive and terraform state
resource "azurerm_storage_account" "central" {
  name                     = var.storage_account_name
  resource_group_name      = azurerm_resource_group.central.name
  location                 = azurerm_resource_group.central.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
}

# App Service plan (F1 free for demo, B1+ for production)
resource "azurerm_service_plan" "api" {
  name                = "asp-central-email"
  resource_group_name = azurerm_resource_group.central.name
  location            = azurerm_resource_group.central.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
}

# Central Email API
resource "azurerm_linux_web_app" "api" {
  name                = var.web_app_name
  resource_group_name = azurerm_resource_group.central.name
  location            = azurerm_resource_group.central.location
  service_plan_id     = azurerm_service_plan.api.id
  https_only          = true

  site_config {
    application_stack { dotnet_version = "10.0" }
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT = "Demo"
  }
}

# Service Bus namespace (Basic tier)
resource "azurerm_servicebus_namespace" "events" {
  name                = var.servicebus_namespace_name
  resource_group_name = azurerm_resource_group.central.name
  location            = azurerm_resource_group.central.location
  sku                 = "Basic"
}

# Service Bus queue for email events
resource "azurerm_servicebus_queue" "email_events" {
  name         = "email-events"
  namespace_id = azurerm_servicebus_namespace.events.id

  enable_partitioning = false
  max_size_in_megabytes = 1024
}

# Consumption plan for Azure Function
resource "azurerm_service_plan" "function" {
  name                = "asp-central-function"
  resource_group_name = azurerm_resource_group.central.name
  location            = azurerm_resource_group.central.location
  os_type             = "Linux"
  sku_name            = "Y1"
}

# Storage account for Function App
resource "azurerm_storage_account" "function" {
  name                     = var.function_storage_name
  resource_group_name      = azurerm_resource_group.central.name
  location                 = azurerm_resource_group.central.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

# Azure Function for processing email events
resource "azurerm_linux_function_app" "audit" {
  name                = var.function_app_name
  resource_group_name = azurerm_resource_group.central.name
  location            = azurerm_resource_group.central.location
  service_plan_id     = azurerm_service_plan.function.id
  storage_account_name = azurerm_storage_account.function.name
  storage_account_access_key = azurerm_storage_account.function.primary_access_key

  site_config {
    application_stack { dotnet_version = "10.0", use_dotnet_isolated_runtime = true }
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT = "Demo"
  }
}
