output "api_url" {
  value = "https://${azurerm_linux_web_app.api.default_hostname}"
}

output "function_app_name" {
  value = azurerm_linux_function_app.audit.name
}

output "servicebus_namespace" {
  value = azurerm_servicebus_namespace.events.name
}

output "resource_group" {
  value = azurerm_resource_group.central.name
}
