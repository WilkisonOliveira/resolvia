FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Resolvia.Domain/Resolvia.Domain.csproj src/Resolvia.Domain/
COPY src/Resolvia.Application/Resolvia.Application.csproj src/Resolvia.Application/
COPY src/Resolvia.Infrastructure/Resolvia.Infrastructure.csproj src/Resolvia.Infrastructure/
COPY src/Resolvia.API/Resolvia.API.csproj src/Resolvia.API/

RUN dotnet restore src/Resolvia.API/Resolvia.API.csproj

COPY src/ src/

RUN dotnet publish src/Resolvia.API/Resolvia.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Resolvia.API.dll"]