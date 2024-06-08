# WebSocket Server and Client

このプロジェクトは、WebSocketを使用してサーバーとクライアント間の通信を行うサンプルです。サーバーはクライアントからの接続を受け入れ、メッセージを送受信します。クライアントはサーバーに接続してメッセージを送信し、他のクライアントからのメッセージを受信します。

## プロジェクト構成

- `WebSocketServer`: WebSocketサーバープロジェクト
- `WebSocketClient`: WebSocketクライアントプロジェクト

## 前提条件

- .NET 9.0 SDK がインストールされていること

## セットアップ手順

### 1. リポジトリのクローン

git clone <リポジトリのURL>
cd WebSocketServerTest

2. 必要なパッケージのインストールとビルド
WebSocketServer

```sh

cd WebSocketServer
dotnet build
```

WebSocketClient

```sh

cd WebSocketClient
dotnet build
```

実行方法
WebSocket サーバーの起動

WebSocketServer ディレクトリに移動します。
サーバーを起動します。

```sh
cd WebSocketServer
dotnet run
```

サーバーは、appsettings.json ファイルに指定された IP アドレスとポートでリッスンします。
WebSocket クライアントの起動

WebSocketClient ディレクトリに移動します。
クライアントを起動します。

```sh
cd WebSocketClient
dotnet run
```

クライアントは、appsettings.json ファイルに指定されたサーバーの IP アドレスとポートに接続します。
設定ファイル

両方のプロジェクトには、サーバーの IP アドレスとポートを指定する appsettings.json ファイルが含まれています。
WebSocketServer/appsettings.json

```json
{
  "ServerSettings": {
    "IPAddress": "localhost",
    "Port": "5001"
  }
}
```

WebSocketClient/appsettings.json

```json
{
  "ServerSettings": {
    "IPAddress": "localhost",
    "Port": "5001"
  }
}
```

# 使用方法
クライアントでのコマンド入力

クライアントが起動すると、以下のコマンドを使用できます。
基本コマンド

## ClientID,Message
- 指定されたクライアントIDにメッセージを送信します。
- 例: 12345,Hello there!

## ClientID,key1:value1,key2:value2,...
- 指定されたクライアントIDにキーと値のペアとしてメッセージを送信します。
- 例: 12345,key1:value1,key2:value2
## cmd list
- 接続されているクライアントのリストを表示します。
- 例: cmd list
## end
- クライアントまたはサーバーを終了します。
- 例: end

## 詳細なコマンド使用例
1.特定のクライアントにメッセージを送信:
```sh
12345,Hello there!
```

このコマンドは、クライアントIDが 12345 のクライアントに "Hello there!" というメッセージを送信します。

2.特定のクライアントにキーと値のペアとしてメッセージを送信:
```sh
12345,key1:value1,key2:value2
```

このコマンドは、クライアントIDが 12345 のクライアントに以下のキーと値のペアを送信します。
```sh
key1: value1
key2: value2
```
3.接続されているクライアントのリストを表示:
```sh
cmd list
```
このコマンドは、現在サーバーに接続されているクライアントのIDリストを表示します。

4.クライアントまたはサーバーを終了:
```sh
end
```
クライアントでこのコマンドを入力すると、クライアントはサーバーとの接続を閉じて終了します。サーバーでこのコマンドを入力すると、サーバーはすべての接続を閉じて終了します。

## メッセージの送信と受信
- クライアントがメッセージを送信すると、サーバーはそのメッセージを指定されたターゲットクライアントIDに転送します。
- クライアントはサーバーからのメッセージを受信すると、コンソールに表示します。

### 注意点
- サーバーとクライアントの設定ファイル (appsettings.json) で指定されているIPアドレスとポートが一致していることを確認してください。
- サーバーが起動していない場合、クライアントは最大3回の接続リトライを行います。
- サーバーまたはクライアントを終了するには、end コマンドを入力します。
