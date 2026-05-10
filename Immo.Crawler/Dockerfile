FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Immo.Crawler/Immo.Crawler.csproj", "Immo.Crawler/"]
COPY ["Immo.Data/Immo.Data.csproj", "Immo.Data/"]
RUN dotnet restore "./Immo.Crawler/Immo.Crawler.csproj"
COPY . .
WORKDIR "/src/Immo.Crawler"
RUN dotnet build "./Immo.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Immo.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Immo.Crawler.dll"]
