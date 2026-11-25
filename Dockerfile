# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY BlackSeaAIS/BlackSeaAIS.csproj BlackSeaAIS/
RUN dotnet restore BlackSeaAIS/BlackSeaAIS.csproj

COPY BlackSeaAIS/ BlackSeaAIS/
RUN dotnet publish BlackSeaAIS/BlackSeaAIS.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "BlackSeaAIS.dll"]
