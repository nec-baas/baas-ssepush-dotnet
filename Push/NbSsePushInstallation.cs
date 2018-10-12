using System;
using System.Threading.Tasks;
using System.Net;
using Nec.Nebula.Internal;

namespace Nec.Nebula
{
    /// <summary>
    /// Server Sent Events(SSE) Pushインスタレーションクラス。
    /// SSE Pushのインスタレーション登録/更新/削除/取得を行う。
    /// </summary>
    public class NbSsePushInstallation : NbPushInstallationBase
    {
        private const string KeyPushType = "_pushType";
        private const string KeySse = "_sse";
        private const string KeyUsername = "username";
        private const string KeyPassword = "password";
        private const string KeyUri = "uri";

        /// <summary>
        /// ユーザ名
        /// </summary>
        internal string Username { get; set; }

        /// <summary>
        /// パスワード
        /// </summary>
        internal string Password { get; set; }

        /// <summary>
        /// Server Sent Events(SSE) PushサーバのURI
        /// </summary>
        internal string Uri { get; set; }

        internal static NbSsePushInstallation _sInstance;

        // 排他制御用フィールド
        private static readonly object _lock = new object();

        // ホストID
        internal static string _sHostId = GenerateDeviceToken();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        internal NbSsePushInstallation(NbService service = null) : base(service)
        {
            // ストレージからインスタレーション情報取得
            LoadFromStorage();
        }

        /// <summary>
        /// ストレージからインスタレーション情報を取得する
        /// ストレージに情報がない場合は、各プロパティが初期状態で設定される
        /// デバイストークンがセットされていない場合はセットする
        /// </summary>
        internal override void LoadFromStorage()
        {
            base.LoadFromStorage();

            // デバイストークンセット
            if (string.IsNullOrEmpty(this.DeviceToken))
            {
                SetDeviceToken();
            }
        }

        // デバイストークンをセットする
        private void SetDeviceToken()
        {
            this.DeviceToken = _sHostId;
        }

        /// <summary>
        /// 現在のSSE Pushインスタレーションを取得する
        /// </summary>
        /// <returns>NbSsePushInstallation</returns>
        public static NbSsePushInstallation GetCurrentInstallation()
        {
            lock (_lock)
            {
                if (_sInstance == null)
                {
                    _sInstance = new NbSsePushInstallation();
                }
            }
            return _sInstance;
        }

        /// <summary>
        /// インスタレーションの新規登録/完全上書き更新を行う。
        /// 
        /// 事前に購読するチャネルの一覧とインスタレーションに対して Push を送信可能なユーザ・グループを設定する必要がある。
        /// インスタレーション情報を更新する前にAcquireLock()にて更新権利の取得を行う必要がある。
        /// </summary>
        /// <returns>NbSsePushInstallation</returns>
        /// <exception cref="InvalidOperationException">インスタレーションのパラメータ未設定、インスタレーションの保存・削除失敗</exception>
        /// <exception cref="NbHttpException">パラメータ未設定、リクエスト送信失敗</exception>
        public async Task<NbSsePushInstallation> Save()
        {
            // アプリが設定可能な必須パラメータ(Channels, AllowedSenders)のnullチェック
            if (this.Channels == null || this.AllowedSenders == null)
            {
                throw new InvalidOperationException("Null Channels or null AllowedSenders.");
            }

            // DeviceTokenはアプリが設定するわけではないので、上とはチェックを分ける
            // DeviceTokenがnullであるはずはないがチェック
            if (this.DeviceToken == null)
            {
                throw new InvalidOperationException("Null DeviceToken.");
            }

            try
            {
                // リクエスト生成＆送信
                var json = await SendRequestForSave();

                // ストレージに保存
                SaveAndLoad(json);
            }
            catch (NbHttpException e)
            {
                // 該当するインスタレーションが存在しない場合
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    // ストレージ内のインスタレーション情報を削除
                    DeleteAndLoad();
                }
                throw e;
            }
            return this;
        }

        /// <summary>
        /// インスタレーション情報をNebulaサーバから取得する
        /// </summary>
        /// <returns>NbSsePushInstallation</returns>
        /// <exception cref="InvalidOperationException">インスタレーションのパラメータ未設定、インスタレーション情報の保存・削除失敗</exception>
        /// <exception cref="NbHttpException">リクエスト送信失敗</exception>
        public static async Task<NbSsePushInstallation> RefreshCurrentInstallation()
        {
            NbSsePushInstallation installation = NbSsePushInstallation.GetCurrentInstallation();

            NbUtil.NotNullWithInvalidOperation(installation.InstallationId, "Null installationId");

            try
            {
                // リクエスト生成＆送信
                var json = await installation.SendRequestForRefresh();

                // ストレージに保存
                installation.SaveAndLoad(json);
            }
            catch (NbHttpException e)
            {
                // 該当するインスタレーションが存在しない場合
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    // ストレージ内のインスタレーション情報を削除
                    installation.DeleteAndLoad();
                }
                throw e;
            }
            return installation;
        }

        /// <summary>
        /// インスタレーションを削除する
        /// </summary>
        /// <returns>Task</returns>
        /// <exception cref="InvalidOperationException">インスタレーションが存在しない</exception>
        /// <exception cref="NbHttpException">リクエスト送信失敗</exception>
        public override async Task DeleteInstallation()
        {
            await base.DeleteInstallation();
        }

        /// <summary>
        /// インスタレーション情報からリクエストボディを生成する。
        /// </summary>
        internal override NbJsonObject MakeRequestBody()
        {
            NbJsonObject body = base.MakeRequestBody();

            // pushTypeを設定
            body[KeyPushType] = "sse";

            return body;
        }

        /// <summary>
        /// JSONをインスタレーションにセット
        /// </summary>
        /// <param name="json">JSON</param>
        internal override void SetInstallationFromJson(NbJsonObject json)
        {
            base.SetInstallationFromJson(json);

            var sseJson = json.GetJsonObject(KeySse);
            if (sseJson == null)
            {
                this.Username = null;
                this.Password = null;
                this.Uri = null;
            }
            else
            {
                this.Username = sseJson.Opt<string>(KeyUsername, null);
                this.Password = sseJson.Opt<string>(KeyPassword, null);
                this.Uri = sseJson.Opt<string>(KeyUri, null);
            }
        }

        /// <summary>
        /// キーがオプションフィールドかどうかチェックする
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>オプションフィールドの場合はtrue</returns>
        protected override bool IsKeyForOption(string key)
        {
            var isKeyForOption = base.IsKeyForOption(key);

            if (key.Equals(KeySse))
            {
                // SSEキーの場合はfalse
                isKeyForOption = false;
            }
            return isKeyForOption;
        }
    }
}
