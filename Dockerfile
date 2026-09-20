FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TurnoOptTI.Web/TurnoOptTI.Web.csproj", "TurnoOptTI.Web/"]
RUN dotnet restore "TurnoOptTI.Web/TurnoOptTI.Web.csproj"
COPY . .
WORKDIR "/src/TurnoOptTI.Web"
RUN dotnet build "TurnoOptTI.Web.csproj" -c Release -o /app/build
RUN dotnet publish "TurnoOptTI.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "TurnoOptTI.Web.dll"]