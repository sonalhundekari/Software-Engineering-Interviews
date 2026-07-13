FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Model/Model.csproj Model/
COPY Serving/Serving.csproj Serving/
RUN dotnet restore Serving/Serving.csproj
COPY Model/ Model/
COPY Serving/ Serving/
RUN dotnet publish Serving/Serving.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
# Model artifacts trained offline via `dotnet run --project Model`
COPY Model/bin/Release/net8.0/fraud_model.dat ./fraud_model.dat
COPY Model/bin/Release/net8.0/scaler.json ./scaler.json
ENV MODEL_PATH=/app/fraud_model.dat
ENV SCALER_PATH=/app/scaler.json
EXPOSE 8080
ENTRYPOINT ["dotnet", "Serving.dll"]
