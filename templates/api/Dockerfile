# Etiquetas = versión de .NET del proyecto (TargetFramework, standards/16)
ARG DOTNET_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/KitApi.Api/KitApi.Api.csproj", "src/KitApi.Api/"]
RUN dotnet restore "src/KitApi.Api/KitApi.Api.csproj"
COPY . .
RUN dotnet publish "src/KitApi.Api/KitApi.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
USER $APP_UID
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KitApi.Api.dll"]
