FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY Bills.sln ./
COPY src/Utilities/Utilities.csproj src/Utilities/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Client/Client.csproj src/Client/
COPY tests/Tests/Tests.csproj tests/Tests/
RUN dotnet restore Bills.sln

FROM restore AS build
COPY src/ src/
RUN dotnet build src/Client/Client.csproj -c Release

FROM build AS publish
RUN dotnet publish src/Client/Client.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Client.dll"]
