FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Discord.csproj を探してリストア
COPY *.csproj ./
RUN dotnet restore

# すべてをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# 実行
ENTRYPOINT ["sh", "-c", "dotnet Discord.dll"]
