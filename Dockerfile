FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# プロジェクトファイルをコピー
# あなたのcsproj名は「Discord.csproj」です
COPY Discord.csproj ./
RUN dotnet restore

# すべてのソースコードをコピー
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# 実行
ENTRYPOINT ["dotnet", "Discord.dll"]
