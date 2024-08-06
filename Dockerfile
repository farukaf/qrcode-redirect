#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/qr-redirect/qr-redirect.csproj", "qr-redirect/"]

RUN dotnet restore "qr-redirect/qr-redirect.csproj"
COPY . .
WORKDIR "src/qr-redirect"
RUN dotnet build "qr-redirect.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "qr-redirect.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "qr-redirect.dll"]