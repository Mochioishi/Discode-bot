# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# すべてをコピー（サブフォルダも含む）
COPY . ./

# プロジェクトファイルを明示的に指定してリストア
# ※ Discode-main.csproj の部分は、実際のファイル名（大文字小文字含む）に合わせてください
RUN dotnet restore *.csproj

# ビルドとリリース
RUN dotnet publish -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 実行ファイル名を確認してください（プロジェクト名.dll）
ENTRYPOINT ["dotnet", "Discode-main.dll"]
