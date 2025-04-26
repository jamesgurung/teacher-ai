ARG APP_NAME=OrgAI
ARG DOTNET_VERSION=10.0-preview-trixie-slim

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY ${APP_NAME}.csproj .
RUN dotnet restore -r linux-x64
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish -r linux-x64 --no-restore --no-self-contained --nologo

FROM base AS final
WORKDIR /app
ARG GITHUB_RUN_NUMBER
ENV GITHUB_RUN_NUMBER=$GITHUB_RUN_NUMBER
COPY --from=publish /app/publish .
ENTRYPOINT dotnet ${APP_NAME}.dll