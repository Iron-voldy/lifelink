FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY LifeLink.sln ./
COPY LifeLink.Api/LifeLink.Api.csproj LifeLink.Api/
COPY LifeLink.Application/LifeLink.Application.csproj LifeLink.Application/
COPY LifeLink.Domain/LifeLink.Domain.csproj LifeLink.Domain/
COPY LifeLink.Infrastructure/LifeLink.Infrastructure.csproj LifeLink.Infrastructure/
COPY LifeLink.Tests/LifeLink.Tests.csproj LifeLink.Tests/
RUN dotnet restore LifeLink.sln
COPY . .
RUN dotnet publish LifeLink.Api/LifeLink.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "LifeLink.Api.dll"]
