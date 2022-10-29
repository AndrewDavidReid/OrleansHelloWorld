FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 30000
EXPOSE 11111

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["HelloWorld.Services/HelloWorld.Services.csproj", "HelloWorld.Services/"]
COPY ["HelloWorld.Abstractions/HelloWorld.Abstractions.csproj", "HelloWorld.Abstractions/"]
COPY ["Orleans.Azure.Infrastructure/Orleans.Azure.Infrastructure.csproj", "Orleans.Azure.Infrastructure/"]
RUN dotnet restore "HelloWorld.Services/HelloWorld.Services.csproj"
COPY . .
WORKDIR "/src/HelloWorld.Services"
RUN dotnet build "HelloWorld.Services.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HelloWorld.Services.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HelloWorld.Services.dll"]
