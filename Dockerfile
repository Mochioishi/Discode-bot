FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 同じフォルダにあるプロジェクトファイルをコピー
COPY *.csproj ./
RUN dotnet restore

# すべてのソースをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# プロジェクト名が Discode-main.csproj なら Discode-main.dll
ENTRYPOINT ["dotnet", "Discode-main.dll"]
