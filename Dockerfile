# 1. ビルド環境（SDKが入っている重いイメージ）
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 設計図（csproj）だけ先にコピーして、ライブラリをダウンロード
# これを分けることで、コード修正時のビルドが速くなる
COPY *.csproj ./
RUN dotnet restore

# プログラム本体をコピーして、実行用ファイル（DLL）を出力
COPY . ./
RUN dotnet publish -c Release -o out

# 2. 実行環境（実行に必要なものだけの軽いイメージ）
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# ビルド環境で作った成果物（outフォルダ）だけを持ってくる
COPY --from=build /app/out .

# Botを起動
# sh -c を使うことで環境変数の展開などがしやすくなる
ENTRYPOINT ["sh", "-c", "dotnet Discord.dll"]
