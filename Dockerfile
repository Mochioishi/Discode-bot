FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 同じフォルダにあるプロジェクトファイルをコピー
# あなたのcsproj名は「Discode-main.csproj」です
COPY Discode-main.csproj ./
RUN dotnet restore

# すべてのソースをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# 実行ファイル名はプロジェクト名に基づきます
ENTRYPOINT ["dotnet", "Discode-main.dll"]
