# Thin wrapper over ./deploy.sh — see README.md for the full flow.
.DEFAULT_GOAL := help
.PHONY: help up up-frontend down clean logs ps build test k8s-up k8s-down

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | \
	  awk 'BEGIN {FS = ":.*?## "}; {printf "  %-14s %s\n", $$1, $$2}'

up: ## Start the stack (infra + services) with Docker Compose
	./deploy.sh up

up-frontend: ## Start the stack including the Angular SPA
	./deploy.sh up --frontend

down: ## Stop the compose stack
	./deploy.sh down

clean: ## Stop and remove volumes (wipes data)
	./deploy.sh clean

logs: ## Tail logs (make logs S=gateway)
	./deploy.sh logs $(S)

ps: ## Show container status
	./deploy.sh ps

build: ## Build the .NET solution
	dotnet build ebanking.sln

test: ## Run the .NET tests
	dotnet test ebanking.sln

k8s-up: ## Build images and deploy to the current kube-context
	./deploy.sh k8s-up

k8s-down: ## Remove the Kubernetes resources
	./deploy.sh k8s-down
