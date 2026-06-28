FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ReliablePaymentProcessing.ProviderMock/*.csproj ReliablePaymentProcessing.ProviderMock/
COPY ReliablePaymentProcessing.Application/*.csproj ReliablePaymentProcessing.Application/
COPY ReliablePaymentProcessing.Domain/*.csproj ReliablePaymentProcessing.Domain/
RUN dotnet restore ReliablePaymentProcessing.ProviderMock/ReliablePaymentProcessing.ProviderMock.csproj

COPY . .
RUN dotnet publish ReliablePaymentProcessing.ProviderMock/ReliablePaymentProcessing.ProviderMock.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReliablePaymentProcessing.ProviderMock.dll"]
