# Orleans Deployment Testing

az group create --location eastus --name orleans-deployment

az deployment group create --name orleans-deployment --resource-group orleans-deployment --template-file main.json

docker buildx build --platform linux/amd64 -t hello-silo .

docker tag xxxx iac-talk/hello-silo:v1
docker tag xxxx iac-talk/hello-api:v1
