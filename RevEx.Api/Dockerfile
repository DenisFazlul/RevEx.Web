FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["RevEx.Domain/RevEx.Domain.csproj", "RevEx.Domain/"]
COPY ["RevEx.Application/RevEx.Application.csproj", "RevEx.Application/"]
COPY ["RevEx.Infrastructure/RevEx.Infrastructure.csproj", "RevEx.Infrastructure/"]
COPY ["RevEx.Api/RevEx.Api.csproj", "RevEx.Api/"]
RUN dotnet restore "RevEx.Api/RevEx.Api.csproj"

COPY . .
RUN dotnet publish "RevEx.Api/RevEx.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RevEx.Api.dll"]
