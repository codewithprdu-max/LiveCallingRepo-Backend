# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY LiveSwitch.API.csproj ./
RUN dotnet restore LiveSwitch.API.csproj

# Copy remaining source code and publish
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 5055

ENV ASPNETCORE_URLS=http://+:5055
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LiveSwitch.API.dll"]

