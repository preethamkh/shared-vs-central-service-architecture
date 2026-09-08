variable "subscription_id" {
  type = string
}

variable "resource_group_name" {
  type    = string
  default = "rg-central-email-service"
}

variable "location" {
  type    = string
  default = "australiaeast"
}

variable "storage_account_name" {
  type = string
}

variable "web_app_name" {
  type = string
}

variable "servicebus_namespace_name" {
  type = string
}

variable "function_app_name" {
  type = string
}

variable "function_storage_name" {
  type = string
}

variable "app_service_sku" {
  type    = string
  default = "F1"
}
