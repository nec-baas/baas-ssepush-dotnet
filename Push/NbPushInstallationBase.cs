using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using Nec.Nebula.Internal;
using System.IO;

namespace Nec.Nebula
{
    /// <summary>
    /// Pushインスタレーション 基底クラス
    /// </summary>
    public abstract class NbPushInstallationBase
    {
        //インスタレーション情報を保存するストレージ
        private static string InstallationFilename = Application.LocalUserAppDataPath + "\\InstallationSettings.config";
        private const string KeyOsType = "_osType";
        private const string KeyOsVersion = "_osVersion";
        private const string KeyDeviceToken = "_deviceToken";
        private const string KeyPushType = "_pushType";
        private const string KeyChannels = "_channels";
        private const string KeyAppVersionCode = "_appVersionCode";
        private const string KeyAppVersionString = "_appVersionString";
        private const string KeyAllowedSenders = "_allowedSenders";
        private const string KeyOwner = "_owner";
        private const string KeyId = "_id";
        private const string KeyOptions = "options"; //REST APIには出さないキー

        /// <summary>
        /// このインスタレーションに対してPushを送信可能なユーザ・グループ
        /// </summary>
        public ISet<string> AllowedSenders { get; set; }

        /// <summary>
        /// アプリケーションのバージョンコード
        /// </summary>
        public int AppVersionCode { get; private set; }

        /// <summary>
        /// アプリケーションのバージョン
        /// </summary>
        public string AppVersionString { get; private set; }

        /// <summary>
        /// 購読するチャネルの一覧
        /// </summary>
        public ISet<string> Channels { get; set; }

        /// <summary>
        /// Device Token
        /// </summary>
        public string DeviceToken { get; internal set; }

        /// <summary>
        /// インスタレーションID
        /// </summary>
        public string InstallationId { get; internal set; }

        /// <summary>
        /// 任意のKey-Value
        /// </summary>
        public NbJsonObject Options { get; set; }

        /// <summary>
        /// OS種別
        /// </summary>
        public string OsType { get; private set; }

        /// <summary>
        /// OSバージョン
        /// </summary>
        public string OsVersion { get; private set; }

        /// <summary>
        /// オーナー情報
        /// </summary>
        public string Owner { get; private set; }

        /// <summary>
        /// 使用するPushテクノロジ
        /// </summary>
        public string PushType { get; protected set; }

        /// <summary>
        /// NbService
        /// </summary>
        internal NbService Service { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="service">サービス</param>
        protected NbPushInstallationBase(NbService service = null)
        {
            Service = service ?? NbService.Singleton;
        }

        /// <summary>
        /// インスタレーションを削除する
        /// </summary>
        /// <returns>Task</returns>
        /// <exception cref="InvalidOperationException">インスタレーションが存在しない</exception>
        /// <exception cref="NbHttpException">リクエスト送信失敗</exception>
        public virtual async Task DeleteInstallation()
        {
            NbUtil.NotNullWithInvalidOperation(this.InstallationId, "Null installationId");
            
            var req = Service.RestExecutor.CreateRequest("/push/installations/{installationId}", HttpMethod.Delete)
                .SetUrlSegment("installationId", this.InstallationId);
            try
            {
                await Service.RestExecutor.ExecuteRequest(req);

                // ストレージ内インスタレーション情報削除
                DeleteAndLoad();
            }
            catch (NbHttpException e)
            {
                // 該当するインスタレーションが存在しない場合
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    // ストレージ内インスタレーション情報削除
                    DeleteAndLoad();
                }
                throw e;
            }
        }
        
        /// <summary>
        /// Save用リクエストを生成・送信する。
        /// </summary>
        /// <returns>送信結果</returns>
        /// <exception cref="NbHttpException">リクエスト送信失敗</exception>
        internal async Task<NbJsonObject> SendRequestForSave()
        {
            NbRestRequest request;
            NbJsonObject bodyJson = MakeRequestBody();

            // リクエスト生成
            if (!string.IsNullOrEmpty(this.InstallationId))
            {
                // 上書きの場合
                var fullUpdateJson = new NbJsonObject();
                fullUpdateJson["$full_update"] = bodyJson;
                request = Service.RestExecutor.CreateRequest("/push/installations/{installationId}", HttpMethod.Put)
                    .SetUrlSegment("installationId", this.InstallationId).SetJsonBody(fullUpdateJson);
            }
            else
            {
                // 新規登録の場合
                request =  Service.RestExecutor.CreateRequest("/push/installations", HttpMethod.Post).SetJsonBody(bodyJson);
            }
            // リクエスト送信
            return await Service.RestExecutor.ExecuteRequestForJson(request);
        }

        /// <summary>
        /// Refresh用リクエストを生成・送信する。
        /// </summary>
        /// <returns>送信結果</returns>
        /// <exception cref="NbHttpException">リクエスト送信失敗</exception>
        internal async Task<NbJsonObject> SendRequestForRefresh()
        {
            NbRestRequest request;
            // リクエスト生成
            request = Service.RestExecutor.CreateRequest("/push/installations/{installationId}", HttpMethod.Get)
                .SetUrlSegment("installationId", this.InstallationId);

            // リクエスト送信
            return await Service.RestExecutor.ExecuteRequestForJson(request);
        }

        /// <summary>
        /// インスタレーション情報からリクエストボディを生成する。
        /// </summary>
        internal virtual NbJsonObject MakeRequestBody()
        {
            NbJsonObject body = new NbJsonObject();

            if (this.Options != null)
            {
                foreach (var opt in this.Options)
                {
                    string key = opt.Key;
                    object val = opt.Value;
                    body[key] = val;
                }
            }

            if (this.Channels != null)
            {
                body[KeyChannels] = this.Channels;
            }

            if (this.AllowedSenders != null)
            {
                body[KeyAllowedSenders] = this.AllowedSenders;
            }

            body[KeyOsType] = "dotnet";

            body[KeyOsVersion] = "Unknown";

            if (this.DeviceToken != null)
            {
                body[KeyDeviceToken] = this.DeviceToken;
            }

            body[KeyAppVersionCode] = -1;

            body[KeyAppVersionString] = GetVersionName();

            // PushTypeはサブクラスで設定する

            return body;
        }

        /// <summary>
        /// 現在実行中のアセンブリ参照を取得してバージョンを取得する
        /// </summary>
        /// <returns>アセンブリバージョン</returns>
        private string GetVersionName()
        {
            var asmbl = Assembly.GetEntryAssembly();
            if (asmbl == null)
            {
                return "0";
            }
            else
            {
                var name = asmbl.GetName();
                return name.Version.ToString();
            }
        }

        /// <summary>
        /// ユニークなホストIDを取得する
        /// </summary>
        /// <returns>GUID</returns>
        protected static string GenerateDeviceToken()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// JSON形式のインスタレーション情報をストレージに保存し、その後インスタレーションクラスにセットする
        /// </summary>
        /// <param name="json">インスタレーション情報</param>
        protected void SaveAndLoad(NbJsonObject json)
        {
            SaveJsonToStorage(json);
            LoadFromStorage();
        }

        /// <summary>
        /// ストレージ内情報を削除した後で、インスタレーション情報をロードする。
        /// </summary>
        protected void DeleteAndLoad()
        {
            DeleteStorage();
            LoadFromStorage();
        }

        /// <summary>
        /// ストレージにJSON情報を保存する
        /// </summary>
        /// <param name="json">JSON情報</param>
        /// <exception cref="InvalidOperationException">ファイル保存失敗</exception>
        protected void SaveJsonToStorage(NbJsonObject json)
        {
            if (json == null)
            {
                return;
            }

            // ストレージ内情報を削除
            DeleteStorage();

            // オプションフィールドが存在する場合は、"options"キーに入れる
            var modifiedJson = ConvertJsonToOptionsIncluded(json);

            // ファイルに保存する
            try
            {
                System.IO.File.WriteAllText(InstallationFilename, modifiedJson.ToString());
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Failed to SaveJsonToStorage()");
            }
        }

        /// <summary>
        /// オプションフィールドを検索し、"options"キーに入れる
        /// </summary>
        /// <param name="json">NbJsonObject</param>
        /// <returns>修正されたNbJsonObject</returns>
        protected NbJsonObject ConvertJsonToOptionsIncluded(NbJsonObject json)
        {
            var modifiedJson = new NbJsonObject();
            var option = new NbJsonObject();

            //キーをListに変換する
            List<string> keysList = json.Keys.ToList();

            foreach (string key in keysList)
            {
                if (IsKeyForOption(key))
                {
                    // オプションに格納
                    option.Add(key, json[key]);
                }
                else
                {
                    // modifiedJsonにコピー
                    modifiedJson.Add(key, json[key]);
                }
            }

            // オプションが存在する場合
            if (option.Count() > 0)
            {
                // "options"キーに入れる
                modifiedJson[KeyOptions] = option;

                // 元のキー/値を削除
                List<string> list = option.Keys.ToList();
                foreach(string key in list){
                    modifiedJson.Remove(key);
                }
            }
            return modifiedJson;
        }

        /// <summary>
        /// キーがオプションフィールドかどうかチェックする
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>オプションフィールドの場合はtrue</returns>
        protected virtual bool IsKeyForOption(string key)
        {
            switch (key)
            {
                case KeyOsType:
                case KeyOsVersion:
                case KeyDeviceToken:
                case KeyPushType:
                case KeyAppVersionString:
                case KeyId:
                case KeyOwner:
                case KeyAppVersionCode:
                case KeyChannels:
                case KeyAllowedSenders:
                    return false;

                // オプションフィールドの場合
                default:
                    return true;
            }
        }

        /// <summary>
        /// ストレージからJsonを読み出す
        /// </summary>
        /// <returns>読み出した値</returns>
        protected NbJsonObject GetJsonFromStorage()
        {
            var attributes = File.GetAttributes(InstallationFilename);
            if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                // シンボリックリンクの場合は例外を投げる
                throw new FileNotFoundException();
            }
            var jsonString = System.IO.File.ReadAllText(InstallationFilename);
            return NbJsonObject.Parse(jsonString);
        }

        /// <summary>
        /// ストレージからインスタレーション情報を取得する
        /// ストレージに情報がない場合は、各プロパティが初期状態で設定される
        /// </summary>
        internal virtual void LoadFromStorage()
        {
            NbJsonObject json = new NbJsonObject();

            try
            {
                lock (this)
                {
                    json = GetJsonFromStorage();
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // do nothing
            }
            SetInstallationFromJson(json);
        }

        /// <summary>
        /// JSONをインスタレーションにセット
        /// </summary>
        /// <param name="json">JSON</param>
        internal virtual void SetInstallationFromJson(NbJsonObject json)
        {
            this.OsType = json.Opt<string>(KeyOsType, null);
            this.OsVersion = json.Opt<string>(KeyOsVersion, null);
            this.DeviceToken = json.Opt<string>(KeyDeviceToken, null);
            this.PushType = json.Opt<string>(KeyPushType, null);
            this.Channels = ConvertJsonArrayToSet(json.Opt<NbJsonArray>(KeyChannels, null));
            this.AppVersionCode = json.Opt<int>(KeyAppVersionCode, -1);
            this.AppVersionString = json.Opt<string>(KeyAppVersionString, null);
            this.AllowedSenders = ConvertJsonArrayToSet(json.Opt<NbJsonArray>(KeyAllowedSenders, null));
            this.Owner = json.Opt<string>(KeyOwner, null);
            this.InstallationId = json.Opt<string>(KeyId, null);
            this.Options = json.Opt<NbJsonObject>(KeyOptions, null);
        }
        /// <summary>
        /// ストレージ内情報を削除
        /// </summary>
        protected void DeleteStorage()
        {
            System.IO.File.Delete(InstallationFilename);
        }

        /// <summary>
        /// NbJsonArrayをSetに変換する
        /// </summary>
        /// <param name="ary">NbJsonArray</param>
        /// <returns>Set</returns>
        internal ISet<string> ConvertJsonArrayToSet(NbJsonArray ary)
        {
            var set = new HashSet<string>();
            if (ary != null)
            {
                foreach (var x in ary)
                {
                    set.Add(x as string);
                }
            }
            else
            {
                set = null;
            }
            return set;
        }
    }
}
