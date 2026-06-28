FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ReliablePaymentProcessing.Worker/*.csproj ReliablePaymentProcessing.Worker/
COPY ReliablePaymentProcessing.Application/*.csproj ReliablePaymentProcessing.Application/
COPY ReliablePaymentProcessing.Domain/*.csproj ReliablePaymentProcessing.Domain/
COPY ReliablePaymentProcessing.Infrastructure/*.csproj ReliablePaymentProcessing.Infrastructure/
RUN dotnet restore ReliablePaymentProcessing.Worker/ReliablePaymentProcessing.Worker.csproj

COPY . .
RUN dotnet publish ReliablePaymentProcessing.Worker/ReliablePaymentProcessing.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReliablePaymentProcessing.Worker.dll"]
