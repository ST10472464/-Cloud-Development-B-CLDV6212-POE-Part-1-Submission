FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV AzureWebJobsStorage="UseDevelopmentStorage=true"
EXPOSE 80
ENV FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
CMD ["dotnet", "Cloud Development B CLDV 6212- POE Part 1.dll"]