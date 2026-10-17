FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /src
COPY src/qr-redirect/ ./
RUN dotnet restore qr-redirect.csproj --locked-mode
RUN dotnet publish qr-redirect.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-azurelinux3.0-distroless@sha256:42f3714eaa91aad7843633f943221f3a36f7894b7d93f512177e9757861c024f AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "qr-redirect.dll"]
