FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PaymentFlowCloud.Worker/*.csproj PaymentFlowCloud.Worker/
COPY PaymentFlowCloud.Application/*.csproj PaymentFlowCloud.Application/
COPY PaymentFlowCloud.Domain/*.csproj PaymentFlowCloud.Domain/
COPY PaymentFlowCloud.Infrastructure/*.csproj PaymentFlowCloud.Infrastructure/
RUN dotnet restore PaymentFlowCloud.Worker/PaymentFlowCloud.Worker.csproj

COPY . .
RUN dotnet publish PaymentFlowCloud.Worker/PaymentFlowCloud.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentFlowCloud.Worker.dll"]
