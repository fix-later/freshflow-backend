FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
# Docker images may lag the workstation SDK patch pinned in global.json.
RUN rm -f global.json
RUN dotnet restore FreshFlow.slnx
RUN dotnet publish src/FreshFlow.API/FreshFlow.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "FreshFlow.API.dll"]
