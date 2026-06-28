FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ReliablePaymentProcessing.Api/*.csproj ReliablePaymentProcessing.Api/
COPY ReliablePaymentProcessing.Application/*.csproj ReliablePaymentProcessing.Application/
COPY ReliablePaymentProcessing.Domain/*.csproj ReliablePaymentProcessing.Domain/
COPY ReliablePaymentProcessing.Infrastructure/*.csproj ReliablePaymentProcessing.Infrastructure/
RUN dotnet restore ReliablePaymentProcessing.Api/ReliablePaymentProcessing.Api.csproj

COPY . .
RUN dotnet publish ReliablePaymentProcessing.Api/ReliablePaymentProcessing.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReliablePaymentProcessing.Api.dll"]
