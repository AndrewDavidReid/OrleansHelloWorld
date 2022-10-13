# Orleans Deployment Testing

az group create --location eastus --name orleans-deployment

az deployment group create --name orleans-deployment --resource-group orleans-deployment --template-file main.json

az login (if needed)
az acr login --name orleansdeploymentacr

docker tag xxxx orleansdeploymentacr.azurecr.io/hello-silo:latest
docker tag xxxx orleansdeploymentacr.azurecr.io/hello-api:latest
