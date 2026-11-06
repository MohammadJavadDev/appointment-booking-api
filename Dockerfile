FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project file changes.
COPY Directory.Build.props AppointmentBooking.sln ./
COPY src/AppointmentBooking.Domain/*.csproj src/AppointmentBooking.Domain/
COPY src/AppointmentBooking.Application/*.csproj src/AppointmentBooking.Application/
COPY src/AppointmentBooking.Infrastructure/*.csproj src/AppointmentBooking.Infrastructure/
COPY src/AppointmentBooking.Api/*.csproj src/AppointmentBooking.Api/
RUN dotnet restore src/AppointmentBooking.Api

COPY src/ src/
RUN dotnet publish src/AppointmentBooking.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AppointmentBooking.Api.dll"]
