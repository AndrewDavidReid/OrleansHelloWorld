# Orleans Deployment Testing

# Azure things

az group create --location eastus --name orleans-deployment

az deployment group create --name orleans-deployment --resource-group orleans-deployment --template-file main.json

az webapp deploy --name hello-orleans-silo \
 --resource-group orleans-deployment \
 --clean true --restart true \
 --type zip --src-path artifacts/silo.zip --debug

az webapp deploy --name hello-orleans-api \
 --resource-group orleans-deployment \
 --clean true --restart true \
 --type zip --src-path artifacts/api.zip --debug

## Docker things

docker buildx build -f HelloWorld.Silo/Dockerfile --platform linux/amd64 -t peiandy/hello-silo:v1 . --push

docker buildx build -f HelloWorld.Services/Dockerfile --platform linux/amd64 -t peiandy/hello-api:v1 . --push
