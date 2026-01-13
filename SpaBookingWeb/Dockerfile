# ========= BUILD STAGE =========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SpaBookingWeb/SpaBookingWeb.csproj SpaBookingWeb/
RUN dotnet restore SpaBookingWeb/SpaBookingWeb.csproj

COPY . .
WORKDIR /src/SpaBookingWeb

RUN dotnet publish \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:TreatWarningsAsErrors=false

# ========= RUNTIME STAGE =========
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# (Optional but recommended)
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

ENTRYPOINT ["dotnet", "SpaBookingWeb.dll"]
