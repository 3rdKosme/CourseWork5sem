# ---------- Этап сборки ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Сначала копируются только файлы проектов — слой восстановления пакетов кэшируется
# и не пересобирается при изменении исходников.
COPY src/PeriphShop.Domain/PeriphShop.Domain.csproj src/PeriphShop.Domain/
COPY src/PeriphShop.Application/PeriphShop.Application.csproj src/PeriphShop.Application/
COPY src/PeriphShop.Infrastructure/PeriphShop.Infrastructure.csproj src/PeriphShop.Infrastructure/
COPY src/PeriphShop.Api/PeriphShop.Api.csproj src/PeriphShop.Api/
RUN dotnet restore src/PeriphShop.Api/PeriphShop.Api.csproj

COPY src/ src/
RUN dotnet publish src/PeriphShop.Api/PeriphShop.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Этап выполнения ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Приложение работает от непривилегированного пользователя (раздел 9 ТЗ).
RUN useradd -m -u 1001 appuser
COPY --from=build /app/publish .
USER appuser

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

# Проверка живости процесса (раздел 10.4 ТЗ).
HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=6 \
    CMD ["/bin/sh", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q '200'"]

ENTRYPOINT ["dotnet", "PeriphShop.Api.dll"]
