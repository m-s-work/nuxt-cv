# C# API (ASP.NET Core). Build context: ./api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CvApi/CvApi.csproj CvApi/
RUN dotnet restore CvApi/CvApi.csproj
COPY CvApi/ CvApi/
RUN dotnet publish CvApi/CvApi.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
# /data holds tenants, invites (SQLite) and cookie-signing keys. Owned by the non-root app user,
# so a fresh named volume inherits the permissions.
# git: fetches pinned CV revisions from the CV repository again after they were pruned (§14).
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /data/tenants && chown -R $APP_UID /data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Cv__DataPath=/data
VOLUME /data
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s CMD ["dotnet", "CvApi.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "CvApi.dll"]
