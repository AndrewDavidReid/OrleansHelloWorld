FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 8080
EXPOSE 30000
EXPOSE 11111

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["HelloWorld.Silo/HelloWorld.Silo.csproj", "HelloWorld.Silo/"]
COPY ["HelloWorld.Abstractions/HelloWorld.Abstractions.csproj", "HelloWorld.Abstractions/"]
COPY ["HelloWorld.Entities/HelloWorld.Entities.csproj", "HelloWorld.Entities/"]
COPY ["Orleans.Azure.Infrastructure/Orleans.Azure.Infrastructure.csproj", "Orleans.Azure.Infrastructure/"]
RUN dotnet restore "HelloWorld.Silo/HelloWorld.Silo.csproj"
COPY . .
WORKDIR "/src/HelloWorld.Silo"
RUN dotnet build "HelloWorld.Silo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HelloWorld.Silo.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HelloWorld.Silo.dll"]
