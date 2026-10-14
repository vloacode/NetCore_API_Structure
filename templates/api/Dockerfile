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
# Liveness sin curl (la imagen aspnet no lo trae): GET /health/live por TCP con bash.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/live HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3 && head -1 <&3 | grep -q " 200 "'
ENTRYPOINT ["dotnet", "KitApi.Api.dll"]
