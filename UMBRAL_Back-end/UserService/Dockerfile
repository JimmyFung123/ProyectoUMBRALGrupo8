# syntax=docker/dockerfile:1
# Imagen del microservicio UserService (UMBRAL).
# Contexto de build esperado: ./UMBRAL_Back-end  (para ver los proyectos Shared/)
#   docker build -f UserService/Dockerfile -t umbral-userservice ./UMBRAL_Back-end

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 1) Solo los .csproj primero, para cachear el restore entre builds.
COPY ["UserService/UserService.csproj", "UserService/"]
COPY ["Shared/UMBRAL.Auth/UMBRAL.Auth.csproj", "Shared/UMBRAL.Auth/"]
COPY ["Shared/UMBRAL.Contracts/UMBRAL.Contracts.csproj", "Shared/UMBRAL.Contracts/"]
RUN dotnet restore "UserService/UserService.csproj"

# 2) El resto del código y publicación.
COPY . .
RUN dotnet publish "UserService/UserService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "UserService.dll"]
