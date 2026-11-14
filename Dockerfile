FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
COPY src/qr-redirect/ ./
RUN dotnet restore qr-redirect.csproj --locked-mode
RUN dotnet publish qr-redirect.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-azurelinux3.0-distroless@sha256:bd252bde098c75a41df4c749f73bb14858a08e7f2429d85812a93e177c503731 AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "qr-redirect.dll"]
