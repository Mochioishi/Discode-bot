FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# ルートにある Discord.csproj をコピー
COPY Discord.csproj ./
RUN dotnet restore

# すべてのファイルをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# 実行ファイル名は Discord.dll
ENTRYPOINT ["dotnet", "Discord.dll"]
