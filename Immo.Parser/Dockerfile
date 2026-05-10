FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Immo.Parser/Immo.Parser.csproj", "Immo.Parser/"]
COPY ["Immo.Data/Immo.Data.csproj", "Immo.Data/"]
RUN dotnet restore "./Immo.Parser/Immo.Parser.csproj"
COPY . .
WORKDIR "/src/Immo.Parser"
RUN dotnet build "./Immo.Parser.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Immo.Parser.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Immo.Parser.dll"]
