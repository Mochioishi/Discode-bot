# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# すべてのファイルをコピー
COPY . ./

# すべてのサブフォルダから .csproj を探し出し、復元とビルドを実行
# プロジェクト名を直接指定せず、ワイルドカードを使用します
RUN dotnet restore **/Discord-bot.csproj
RUN dotnet publish **/Discord-bot.csproj -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
# ビルド環境で作られた out フォルダの中身をコピー
COPY --from=build-env /app/out .

# 実行（dll名はプロジェクト名と一致する必要があります）
ENTRYPOINT ["dotnet", "Discord-bot.dll"]
