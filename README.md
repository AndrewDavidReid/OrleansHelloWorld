# Orleans Deployment Testing

az group create --location eastus --name orleans-deployment

az deployment group create --name orleans-deployment --resource-group orleans-deployment --template-file main.json

## Docker things

docker buildx build -f HelloWorld.Silo/Dockerfile --platform linux/amd64 -t peiandy/hello-silo:v1 . --push

docker buildx build -f HelloWorld.Services/Dockerfile --platform linux/amd64 -t peiandy/hello-api:v1 . --push
