using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows.Forms;

namespace Nec.Nebula.Test
{
    [TestFixture]
    public class NbSsePushInstallationTest
    {
        private MockRestExecutor executor;
        private const string appKey = "X-Application-Key";
        private const string appId = "X-Application-Id";
        private const string session = "X-Session-Token";

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
        private const string KeySse = "_sse";
        private const string KeyUsername = "username";
        private const string KeyPassword = "password";
        private const string KeyUri = "uri";

        [SetUp]
        public void SetUp()
        {
            TestUtils.Init();
            DeleteStorage();

            // inject Mock RestExecutor
            executor = new MockRestExecutor();
            NbService.Singleton.RestExecutor = executor;
        }

        [TearDown]
        public void TearDown()
        {
            NbSsePushInstallation._sInstance = null;
        }

        /**
         * LoadFromStorage
        **/

        /// <summary>
        /// ストレージからインスタレーション情報を取得
        /// ストレージにデバイストークンがセットされている場合、同じデバイストークンがセットされること
        /// </summary>
        /// チケット #3400「インスタレーション削除時の初期化処理のコードが理解しにくいので、改善する」に対するテスト
        [Test]
        public void TestLoadFromStorageDeviceTokenExists()
        {
            // ストレージにインスタレーション情報を登録
            SaveInstallationToStorage(true);

            // Main
            NbSsePushInstallation installation = new NbSsePushInstallation();
            installation.LoadFromStorage();

            CheckInstallation(installation, true);
        }

        /// <summary>
        /// ストレージからインスタレーション情報を取得
        /// ストレージにデバイストークンがセットされていない場合、デバイストークンがセットされること
        /// </summary>
        /// チケット #3400「インスタレーション削除時の初期化処理のコードが理解しにくいので、改善する」に対するテスト
        [Test]
        public void TestLoadFromStorageDeviceTokenNotExists()
        {
            // Main
            NbSsePushInstallation installation = new NbSsePushInstallation();
            installation.LoadFromStorage();

            CheckInstallation(installation, false);
            Assert.AreNotEqual("abcdefg", installation.DeviceToken);
        }

        /**
        * GetCurrentInstallation
        **/

        /// <summary>
        /// 現在のSSE Pushインスタレーションを取得（ストレージにインスタレーション情報が存在しない場合）
        /// デバイストークンが設定されていること
        /// デバイストークン以外は初期値が設定されていること
        /// 2回実行しても結果が同じであること
        /// </summary>
        [Test]
        public void TestGetCurrentInstallationNormalNotExists()
        {
            NbSsePushInstallation installation = new NbSsePushInstallation();
            // 2回実行
            for (var i = 0; i < 2; i++)
            {
                installation = NbSsePushInstallation.GetCurrentInstallation();
                CheckInstallation(installation, false);
            }
        }

        /// <summary>
        /// 現在のSSE Pushインスタレーションを取得（ストレージにインスタレーション情報が存在する場合）
        /// デバイストークンが設定されていること
        /// 取得結果がストレージ内のインスタレーション情報と同じであること
        /// 2回実行しても結果が同じであること
        /// </summary>
        [Test]
        public void TestGetCurrentInstallationNormalExists()
        {
            NbSsePushInstallation installation = new NbSsePushInstallation();
            SaveInstallationToStorage(true);
            
            // 2回実行
            for (var i = 0; i < 2; i++)
            {
                installation = NbSsePushInstallation.GetCurrentInstallation();
                CheckInstallation(installation, true);
            }
        }

        private void CheckInstallation(NbSsePushInstallation installation, bool isInstallationExists)
        {
            if (isInstallationExists)
            {
                var installationJson = CreateResponseBody(true);

                Assert.AreEqual(installation.AllowedSenders, installationJson["_allowedSenders"]);
                Assert.AreEqual(installation.AppVersionCode, installationJson["_appVersionCode"]);
                Assert.AreEqual(installation.AppVersionString, installationJson["_appVersionString"]);
                Assert.AreEqual(installation.Channels, installationJson["_channels"]);
                Assert.AreEqual(installation.DeviceToken, installationJson["_deviceToken"]);
                Assert.AreEqual(installation.InstallationId, installationJson["_id"]);
                Assert.AreEqual(installation.Options["option1"], installationJson["option1"]);
                Assert.AreEqual(installation.Options["option2"], installationJson["option2"]);
                Assert.AreEqual(installation.OsType, installationJson["_osType"]);
                Assert.AreEqual(installation.OsVersion, installationJson["_osVersion"]);
                Assert.AreEqual(installation.Owner, installationJson["_owner"]);
                Assert.AreEqual(installation.PushType, installationJson["_pushType"]);
                NbJsonObject sse = (NbJsonObject)installationJson["_sse"];
                Assert.AreEqual(installation.Username, sse["username"]);
                Assert.AreEqual(installation.Password, sse["password"]);
                Assert.AreEqual(installation.Uri, sse["uri"]);
            }
            else
            {
                Assert.IsNull(installation.AllowedSenders);
                Assert.AreEqual(installation.AppVersionCode, -1);
                Assert.IsNull(installation.AppVersionString);
                Assert.IsNull(installation.Channels);
                Assert.IsNotNull(installation.DeviceToken);
                Assert.IsNull(installation.InstallationId);
                Assert.IsNull(installation.Options);
                Assert.IsNull(installation.OsType);
                Assert.IsNull(installation.OsVersion);
                Assert.IsNull(installation.Owner);
                Assert.IsNull(installation.PushType);
            }
        }

        /**
        * Save
        **/

        /// <summary>
        /// インスタレーションの新規登録/完全上書き更新
        /// リクエスト、レスポンスの情報が正しいこと
        /// ストレージにインスタレーション情報が保存されていること
        /// </summary>
        [Test]
        public async void TestSaveNormal()
        {
            // インスタレーションに必須パラメータとオプションをセット
            NbSsePushInstallation installation = SetInstallationParameterAndOption();

            var responseBody = CreateResponseBody(true);
            var response = new MockRestResponse(HttpStatusCode.OK, responseBody.ToString());
            executor.AddResponse(response);

            // Main
            var result = await installation.Save();

            // Check Response
            CheckInstallation(result);
            // インスタレーション内容チェック
            CheckInstallation(installation);

            // Check Request
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan0");
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:group1");

            var req = executor.LastRequest;
            var reqJson = NbJsonParser.Parse(req.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(HttpMethod.Post, req.Method);
            Assert.IsTrue(req.Uri.EndsWith("/installations"));
            Assert.AreEqual(3, req.Headers.Count);
            Assert.IsTrue(req.Headers.ContainsKey(appKey));
            Assert.IsTrue(req.Headers.ContainsKey(appId));
            Assert.AreEqual(reqJson["_osType"], "dotnet");
            Assert.AreEqual(reqJson["_osVersion"], "Unknown");
            Assert.AreEqual(reqJson["_deviceToken"], "abcdefg");
            Assert.AreEqual(reqJson["_pushType"], "sse");
            Assert.AreEqual(reqJson["_channels"], channels);
            Assert.AreEqual(reqJson["_appVersionCode"], -1);
            // NUnitの場合は"0"が設定される
            Assert.AreEqual(reqJson["_appVersionString"], "0");
            Assert.AreEqual(reqJson["_allowedSenders"], allowedSenders);
            Assert.AreEqual(reqJson["option1"], "option1value");
            Assert.AreEqual(reqJson["option2"], "option2value");

            // ストレージ内のインスタレーション情報存在チェック
            CheckSaveStorage();


        }

        /// <summary>
        /// インスタレーションの新規登録/完全上書き更新(異常)
        /// Channelsがnullの場合、InvalidOperationExceptionが返ること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public async void TestSaveExceptionChanneslNull()
        {
            // インスタレーションに必須パラメータとオプションをセット
            NbSsePushInstallation installation = SetInstallationParameterAndOption();
            installation.Channels = null;

            // Main
            var result = await installation.Save();
            Assert.Fail("No Exception");
        }

        /// <summary>
        /// インスタレーションの新規登録/完全上書き更新(異常)
        /// AllowedSendersがnullの場合、InvalidOperationExceptionが返ること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public async void TestSaveExceptionAllowedSendersNull()
        {
            // インスタレーションに必須パラメータとオプションをセット
            NbSsePushInstallation installation = SetInstallationParameterAndOption();
            installation.AllowedSenders = null;

            // Main
            var result = await installation.Save();
            Assert.Fail("No Exception");
        }

        /// <summary>
        /// インスタレーションの新規登録/完全上書き更新(異常)
        /// DeviceTokenがnullの場合、InvalidOperationExceptionが返ること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public async void TestSaveExceptionNoDeviceToken()
        {
            // インスタレーションに必須パラメータとオプションをセット
            NbSsePushInstallation installation = SetInstallationParameterAndOption();
            installation.DeviceToken = null;

            // Main
            var result = await installation.Save();
            Assert.Fail("No Exception");
        }

        /// <summary>
        /// インスタレーションの新規登録/完全上書き更新(異常)
        /// 該当するインスタレーションが存在しない場合、NbHttpException(NotFound)が返ること
        /// ストレージ内のインスタレーション情報が削除されること
        /// </summary>
        [Test]
        public async void TestSaveExceptionNoInstallationId()
        {
            // インスタレーションに必須パラメータとオプションをセット
            NbSsePushInstallation installation = SetInstallationParameterAndOption();

            var response = new MockRestResponse(HttpStatusCode.NotFound);
            executor.AddResponse(response);

            try
            {
                // Main
                await installation.Save();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            // ストレージ内のインスタレーション情報削除チェック
            CheckDeleteStorage();

            // インスタレーション初期化チェック
            CheckDeleteInstallation(installation);
        }

        /**
        * RefreshCurrentInstallation
        **/

        /// <summary>
        /// インスタレーション情報をNebulaサーバから取得
        /// リクエスト、レスポンスの情報が正しいこと
        /// ストレージにインスタレーション情報が保存されていること
        /// </summary>
        [Test]
        public async void TestRefreshCurrentInstallationNormal()
        {
            // インスタレーションをストレージに保存
            SaveInstallationToStorage(true);

            var responseBody = CreateResponseBody(true);
            var response = new MockRestResponse(HttpStatusCode.OK, responseBody.ToString());
            executor.AddResponse(response);

            // Main
            var installationResp = await NbSsePushInstallation.RefreshCurrentInstallation();

            // Check Response
            CheckInstallation(installationResp);

            // Check Request
            var req = executor.LastRequest;
            Assert.AreEqual(HttpMethod.Get, req.Method);
            Assert.IsTrue(req.Uri.EndsWith("/installations/" + "12345"));
            Assert.AreEqual(3, req.Headers.Count);
            Assert.IsTrue(req.Headers.ContainsKey(appKey));
            Assert.IsTrue(req.Headers.ContainsKey(appId));

            // ストレージ内のインスタレーション情報存在チェック
            CheckSaveStorage();
        }

        /// <summary>
        /// インスタレーション情報をNebulaサーバから取得(異常)
        /// インスタレーションのパラメータ未設定の場合、InvalidOperationExceptionが返ること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public async void TestRefreshCurrentInstallationExceptionNoInstallationId()
        {
            // インスタレーションをストレージに保存(インスタレーションIDなし)
            SaveInstallationToStorage(false);

            // Main
            var installationResp = await NbSsePushInstallation.RefreshCurrentInstallation();
        }

        /// <summary>
        /// インスタレーション情報をNebulaサーバから取得(異常)
        /// 該当するインスタレーションが存在しない場合、NbHttpException(NotFound)が返ること
        /// ストレージ内のインスタレーション情報が削除されること
        /// </summary>
        [Test]
        public async void TestRefreshCurrentInstallationExceptionNotExists()
        {
            NbSsePushInstallation installation = new NbSsePushInstallation();

            // インスタレーションをストレージに保存
            SaveInstallationToStorage(true);

            var response = new MockRestResponse(HttpStatusCode.NotFound);
            executor.AddResponse(response);

            try
            {
                // Main
                installation = await NbSsePushInstallation.RefreshCurrentInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            // ストレージ内のインスタレーション情報削除チェック
            CheckDeleteStorage();
        }

        /**
        * MakeRequestBody
        **/

        /// <summary>
        /// インスタレーション情報からリクエストボディを生成
        /// リクエストボディが生成されること
        /// </summary>
        [Test]
        public void TestMakeRequestBodyNormal()
        {
            // インスタレーションに必須パラメータとオプションをセット
            NbSsePushInstallation installation = SetInstallationParameterAndOption();

            // Main
            NbJsonObject body = installation.MakeRequestBody();

            // Check Response
            CheckBody(body);
        }

        // インスタレーションに必須パラメータとオプションを設定
        private NbSsePushInstallation SetInstallationParameterAndOption()
        {
            NbSsePushInstallation installation = new NbSsePushInstallation();

            ISet<string> channels = new HashSet<string>();
            channels.Add("chan0");
            installation.Channels = channels;

            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:group1");
            installation.AllowedSenders = allowedSenders;

            installation.DeviceToken = "abcdefg";

            NbJsonObject options = new NbJsonObject();
            options.Add("option1", "option1value");
            options.Add("option2", "option2value");
            installation.Options = options;

            return installation;
        }

        // ストレージ内情報を削除
        private void DeleteStorage()
        {
            System.IO.File.Delete(InstallationFilename);
        }

        /// <summary>
        /// インスタレーション情報を作成してストレージに保存する
        /// </summary>
        /// <param name="flag">インスタレーションIDを付与する場合はtrueを指定する</param>
        private void SaveInstallationToStorage(bool flag)
        {
            var json = new NbJsonObject();
            json = CreateResponseBody(flag);

            // オプションのプロパティがセットされている場合は、"options"キーに入れる
            FindAndSetOptions(json);

            // ファイルに保存する
            try
            {
                System.IO.File.WriteAllText(InstallationFilename, json.ToString());
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Failed to SaveJsonToStorage()");
            }
        }

        private NbJsonObject CreateResponseBody(bool flag)
        {
            NbJsonObject body = new NbJsonObject();

            ISet<string> channels = new HashSet<string>();
            channels.Add("chan0");
            body["_channels"] = channels;

            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:group1");
            body["_allowedSenders"] = allowedSenders;

            body["_osType"] = "dotnet";
            body["_osVersion"] = "Unknown";
            body["_deviceToken"] = "abcdefg";
            body["_appVersionCode"] = -1;
            body["_appVersionString"] = "4.0.0.0";
            body["_pushType"] = "sse";
            if (flag)
            {
                body["_id"] = "12345";
            }
            body["_owner"] = "ownerString";

            // SSE情報付加
            NbJsonObject sse = new NbJsonObject();
            sse["username"] = "testname";
            sse["password"] = "testpass";
            sse["uri"] = "http://example.push.server/foo/bar";
            body["_sse"] = sse;

            // Option付加
            body["option1"] = "option1value";
            body["option2"] = "option2value";

            return body;
        }

        // オプションプロパティを検索し、"options"キーに入れる
        protected void FindAndSetOptions(NbJsonObject json)
        {
            NbJsonObject option = new NbJsonObject();

            //キーをListに変換する
            List<string> keysList = json.Keys.ToList();

            foreach (string key in keysList)
            {
                FindOptions(key, json, option);
            }

            if (option.Count() > 0)
            {
                json[KeyOptions] = option;

                // 元のキー/値を削除
                List<string> list = option.Keys.ToList();
                foreach (string key in list)
                {
                    json.Remove(key);
                }
            }
        }

        protected void FindOptions(string key, NbJsonObject json, NbJsonObject option)
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
                case KeySse:
                    // そのまま
                    break;

                default:
                    // それ以外はオプションとしてセット
                    option.Add(key, json[key]);
                    break;
            }
        }

        // インスタレーション情報を格納するファイルが削除されたかどうかチェック
        private void CheckDeleteStorage()
        {
            try
            {
                System.IO.File.ReadAllText(InstallationFilename);
            }
            catch (System.IO.FileNotFoundException)
            {
                return;
            }
            Assert.Fail("Storage Not Deleted");
        }

        // インスタレーション情報を格納するファイルが保存されたかどうかチェック
        private void CheckSaveStorage()
        {
            try
            {
                System.IO.File.ReadAllText(InstallationFilename);
            }
            catch (System.IO.FileNotFoundException)
            {
                Assert.Fail("Storage Not Found");
            }

            // ストレージに保存されたインスタレーション内容が正しいかどうかチェック
            NbSsePushInstallation installation = new NbSsePushInstallation();
            installation.LoadFromStorage();
            CheckInstallation(installation);
        }

        // インスタレーションが削除されたかどうかチェック
        private void CheckDeleteInstallation(NbSsePushInstallation installation)
        {
            bool isDeleted = true;

            if (installation.OsType != null) isDeleted = false;
            if (installation.OsVersion != null) isDeleted = false;
            if (installation.DeviceToken == null) isDeleted = false;
            if (installation.PushType != null) isDeleted = false;
            if (installation.Channels != null) isDeleted = false;
            if (installation.AppVersionCode != -1) isDeleted = false;
            if (installation.AppVersionString != null) isDeleted = false;
            if (installation.AllowedSenders != null) isDeleted = false;
            if (installation.InstallationId != null) isDeleted = false;
            if (installation.Owner != null) isDeleted = false;
            if (installation.Options != null) isDeleted = false;
            if (installation.Username != null) isDeleted = false;
            if (installation.Password != null) isDeleted = false;
            if (installation.Uri != null) isDeleted = false;

            if (!isDeleted)
            {
                Assert.Fail("Installation Not Deleted");
            }
        }

        // インスタレーションの内容チェック
        private void CheckInstallation(NbSsePushInstallation installation)
        {
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan0");
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:group1");

            Assert.AreEqual(installation.OsType, "dotnet");
            Assert.AreEqual(installation.OsVersion, "Unknown");
            Assert.AreEqual(installation.DeviceToken, "abcdefg");
            Assert.AreEqual(installation.PushType, "sse");
            Assert.AreEqual(installation.Channels, channels);
            Assert.AreEqual(installation.AppVersionCode, -1);
            Assert.AreEqual(installation.AppVersionString, "4.0.0.0");
            Assert.AreEqual(installation.AllowedSenders, allowedSenders);
            Assert.AreEqual(installation.InstallationId, "12345");
            Assert.AreEqual(installation.Owner, "ownerString");
            Assert.AreEqual(installation.Username, "testname");
            Assert.AreEqual(installation.Password, "testpass");
            Assert.AreEqual(installation.Uri, "http://example.push.server/foo/bar");
            Assert.AreEqual(installation.Options["option1"], "option1value");
            Assert.AreEqual(installation.Options["option2"], "option2value");
        }

        // リクエストボディ内容をチェック
        private void CheckBody(NbJsonObject body)
        {
            Assert.AreEqual(body["option1"], "option1value");
            Assert.AreEqual(body["option2"], "option2value");
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan0");
            Assert.AreEqual(body["_channels"], channels);

            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:group1");
            body["_allowedSenders"] = allowedSenders;

            body["_osType"] = "dotnet";
            body["_osVersion"] = "Unknown";
            body["_deviceToken"] = "abcdefg";
            body["_appVersionCode"] = -1;
            body["_appVersionString"] = "0";
            body["_pushType"] = "sse";
        }
    }
}
