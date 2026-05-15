FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PaymentFlowCloud.Api/*.csproj PaymentFlowCloud.Api/
COPY PaymentFlowCloud.Application/*.csproj PaymentFlowCloud.Application/
COPY PaymentFlowCloud.Domain/*.csproj PaymentFlowCloud.Domain/
COPY PaymentFlowCloud.Infrastructure/*.csproj PaymentFlowCloud.Infrastructure/
RUN dotnet restore PaymentFlowCloud.Api/PaymentFlowCloud.Api.csproj

COPY . .
RUN dotnet publish PaymentFlowCloud.Api/PaymentFlowCloud.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentFlowCloud.Api.dll"]
