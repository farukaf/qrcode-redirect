FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src
COPY src/qr-redirect/ ./
RUN dotnet restore qr-redirect.csproj --locked-mode
RUN dotnet publish qr-redirect.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-azurelinux3.0-distroless@sha256:603c3f61189d5252ceb2c169b9e6920e44c2aa0e1c47a5fce3b29e89bd880260 AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "qr-redirect.dll"]
