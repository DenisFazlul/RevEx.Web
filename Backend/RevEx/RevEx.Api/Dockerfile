FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RevEx.Domain/ RevEx.Domain/
COPY RevEx.Web/Backend/RevEx/ RevEx.Web/Backend/RevEx/
WORKDIR /src/RevEx.Web/Backend/RevEx
RUN dotnet publish RevEx.Api/RevEx.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RevEx.Api.dll"]
