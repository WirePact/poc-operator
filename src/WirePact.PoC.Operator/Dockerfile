# Build the operator
FROM mcr.microsoft.com/dotnet/sdk:latest as build
WORKDIR /operator

COPY ./ ./
RUN dotnet publish -c Release -o out src/WirePact.PoC.Operator/WirePact.PoC.Operator.csproj

# The runner for the application
FROM mcr.microsoft.com/dotnet/aspnet:latest as final
LABEL org.opencontainers.image.source=https://github.com/WirePact/poc-operator

WORKDIR /operator

COPY --from=build /operator/out/ ./

ENTRYPOINT [ "dotnet", "WirePact.PoC.Operator.dll" ]
