FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# プロジェクトファイル名は Discode.csproj です
COPY Discode.csproj ./
RUN dotnet restore

# ソースコードをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# 実行ファイル名は Discode.dll です
ENTRYPOINT ["dotnet", "Discode.dll"]
