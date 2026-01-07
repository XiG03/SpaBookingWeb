# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["SpaBookingWeb/SpaBookingWeb.csproj", "SpaBookingWeb/"]
RUN dotnet restore "SpaBookingWeb/SpaBookingWeb.csproj"

COPY . .
WORKDIR "/src/SpaBookingWeb"
RUN dotnet publish -c Release -o /app/publish /p:TreatWarningsAsErrors=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SpaBookingWeb.dll"]
