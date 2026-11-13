# Build with official NuGet dependencies. Do not pass UseExternalSqlProvider=true.
# The workspace bundle is compiled first so the ASP.NET Core host ships the UI + API
# from one process (docs/adr/0007). The nginx stage remains available as an optional
# edge for split deployments via --target workspace.
FROM node:22-bookworm-slim AS web-build
WORKDIR /web
COPY src/Careflow.Web/package*.json ./
RUN npm ci
COPY src/Careflow.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json Careflow.slnx ./
COPY src/Careflow.Domain/Careflow.Domain.csproj src/Careflow.Domain/
COPY src/Careflow.Application/Careflow.Application.csproj src/Careflow.Application/
COPY src/Careflow.Infrastructure/Careflow.Infrastructure.csproj src/Careflow.Infrastructure/
COPY src/Careflow.Api/Careflow.Api.csproj src/Careflow.Api/
COPY tests/Careflow.Tests/Careflow.Tests.csproj tests/Careflow.Tests/
COPY tests/Careflow.SqlTests/Careflow.SqlTests.csproj tests/Careflow.SqlTests/
RUN dotnet restore Careflow.slnx
COPY src/ src/
COPY tests/ tests/
COPY --from=web-build /web/dist/ src/Careflow.Web/dist/
RUN dotnet publish src/Careflow.Api/Careflow.Api.csproj -c Release -o /publish --no-restore

# Optional TLS-edge companion image for split deployments: docker build --target workspace.
FROM nginxinc/nginx-unprivileged:stable-alpine AS workspace
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=web-build /web/dist/ /usr/share/nginx/html/
EXPOSE 8080

# Default final image: ASP.NET Core serves the workspace UI and the API from one process.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /publish ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 Careflow__Mode=SqlServer
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Careflow.Api.dll"]
