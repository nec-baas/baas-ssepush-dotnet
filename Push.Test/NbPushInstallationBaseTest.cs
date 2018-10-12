using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Windows.Forms;

namespace Nec.Nebula.Test
{
    [TestFixture]
    public class NbPushInstallationBaseTest
    {
        private MockRestExecutor executor;
        private const string appKey = "X-Application-Key";
        private const string appId = "X-Application-Id";
        private const string session = "X-Session-Token";

        private static string InstallationFilename = Application.LocalUserAppDataPath + "\\InstallationSettings.config";

        NbPushInstallationBase installation;

        // 継承クラス
        private class NbPushInstallation : NbPushInstallationBase
        {
            public NbPushInstallation(NbService service) : base(service)
            {
            }
        }

        [SetUp]
        public void SetUp()
        {
            TestUtils.Init();
            DeleteStorage();

            // inject Mock RestExecutor
            executor = new MockRestExecutor();
            NbService.Singleton.RestExecutor = executor;

            installation = new NbPushInstallation(null);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteStorage();
        }

        /**
         * Constructor(NbPushInstallationBase)
         **/

        /// <summary>
        /// コンストラクタ（正常）
        /// Serviceが設定されること。
        /// Service以外のプロパティが初期値であること。
        /// </summary>
        [Test]
        public void TestConstructorNormal()
        {
            // Main
            var installation = new NbPushInstallation(null);

            // Assert
            Assert.AreEqual(NbService.Singleton, installation.Service);
            Assert.IsNull(installation.AllowedSenders);
            Assert.AreEqual(installation.AppVersionCode, 0);
            Assert.IsNull(installation.AppVersionString);
            Assert.IsNull(installation.Channels);
            Assert.IsNull(installation.DeviceToken);
            Assert.IsNull(installation.InstallationId);
            Assert.IsNull(installation.Options);
            Assert.IsNull(installation.OsType);
            Assert.IsNull(installation.OsVersion);
            Assert.IsNull(installation.Owner);
            Assert.IsNull(installation.PushType);
        }

        /// <summary>
        /// コンストラクタ（サービス指定）（正常）
        /// 指定したServiceが設定されること。
        /// Service以外のプロパティが初期値であること。
        /// </summary>
        [Test]
        public void TestConstructorWithServiceNormal()
        {
            NbService.EnableMultiTenant(true);
            var service = NbService.GetInstance();

            // Main
            var installation = new NbPushInstallation(service);

            // Assert
            Assert.AreEqual(service, installation.Service);
            Assert.IsNull(installation.AllowedSenders);
            Assert.AreEqual(installation.AppVersionCode, 0);
            Assert.IsNull(installation.AppVersionString);
            Assert.IsNull(installation.Channels);
            Assert.IsNull(installation.DeviceToken);
            Assert.IsNull(installation.InstallationId);
            Assert.IsNull(installation.Options);
            Assert.IsNull(installation.OsType);
            Assert.IsNull(installation.OsVersion);
            Assert.IsNull(installation.Owner);
            Assert.IsNull(installation.PushType);

            NbService.EnableMultiTenant(false);
        }

        /**
        * DeleteInstallation
        **/

        /// <summary>
        /// インスタレーション削除（正常）
        /// リクエストの情報が正しいこと
        /// ストレージからインスタレーション情報が削除されること
        /// インスタレーション情報が削除(初期化)されること
        /// </summary>
        [Test]
        public async void TestDeleteInstallationNormal()
        {
            SaveInstallationToStorage(true);
            installation.LoadFromStorage();
            var installationId = installation.InstallationId;

            var response = new MockRestResponse(HttpStatusCode.OK);
            executor.AddResponse(response);

            // Main
            await installation.DeleteInstallation();

            // Check Request
            var req = executor.LastRequest;
            Assert.AreEqual(HttpMethod.Delete, req.Method);
            Assert.IsTrue(req.Uri.EndsWith("/installations/" + installationId));
            Assert.AreEqual(3, req.Headers.Count);
            Assert.IsTrue(req.Headers.ContainsKey(appKey));
            Assert.IsTrue(req.Headers.ContainsKey(appId));

            // Check Delete Storage
            CheckDeleteStorage();

            // Check Delete Installation
            CheckDeleteInstallation(installation);
        }


        /// <summary>
        /// インスタレーション削除（インスタレーションIDが存在しない）
        /// InvalidOperationExceptionが発行されること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public async void TestDeleteInstallationExceptionNoInstallationId()
        {
            SaveInstallationToStorage(false);
            installation.LoadFromStorage();

            // Main
            await installation.DeleteInstallation();

            Assert.Fail("No Exception");
        }

        /// <summary>
        /// インスタレーション削除（サーバに該当するインスタレーションが存在しない）
        /// NbHttpException(NotFound)が発行されること
        /// ストレージからインスタレーション情報が削除されること
        /// インスタレーション情報が削除(初期化)されること
        /// </summary>
        [Test]
        public async void TestDeleteInstallationExceptionNoInstallationOnServer()
        {
            SaveInstallationToStorage(true);
            installation.LoadFromStorage();

            var response = new MockRestResponse(HttpStatusCode.NotFound);
            executor.AddResponse(response);

            try
            {
                // Main
                await installation.DeleteInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);

                // Check Delete Storage
                CheckDeleteStorage();

                // Check Delete Installation
                CheckDeleteInstallation(installation);
            }
        }

        /// <summary>
        /// インスタレーション削除（異常）
        /// NbHttpExceptionが発行されること
        /// </summary>
        [Test]
        public async void TestDeleteInstallationExceptionFailer()
        {
            SaveInstallationToStorage(true);
            installation.LoadFromStorage();

            var response = new MockRestResponse(HttpStatusCode.Forbidden);
            executor.AddResponse(response);

            try
            {
                // Main
                await installation.DeleteInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.Forbidden);
            }
        }

        /**
        * SendRequestForSave
        **/

        /// <summary>
        /// Save用リクエストを生成・送信（正常）
        /// リクエスト、レスポンスの情報が正しいこと
        /// </summary>
        [Test]
        public async void TestSendRequestForSaveNormal()
        {
            SaveInstallationToStorage(false);
            installation.LoadFromStorage();

            var responseBody = CreateResponseBody(false);
            var response = new MockRestResponse(HttpStatusCode.OK, responseBody.ToString());
            executor.AddResponse(response);

            // Main
            var result = await installation.SendRequestForSave();

            // Check Response
            Assert.AreEqual(result, responseBody);

            // Check Request
            var req = executor.LastRequest;
            var reqJson = NbJsonParser.Parse(req.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(HttpMethod.Post, req.Method);
            Assert.IsTrue(req.Uri.EndsWith("/installations"));
            Assert.AreEqual(3, req.Headers.Count);
            Assert.IsTrue(req.Headers.ContainsKey(appKey));
            Assert.IsTrue(req.Headers.ContainsKey(appId));
            Assert.AreEqual(reqJson["_channels"], installation.Channels);
            Assert.AreEqual(reqJson["_allowedSenders"], installation.AllowedSenders);
            Assert.AreEqual(reqJson["_osType"], installation.OsType);
            Assert.AreEqual(reqJson["_osVersion"], installation.OsVersion);
            Assert.AreEqual(reqJson["_deviceToken"], installation.DeviceToken);
            Assert.AreEqual(reqJson["_appVersionCode"], installation.AppVersionCode);
            // NUnitの場合は"0"が設定される
            Assert.AreEqual(reqJson["_appVersionString"], "0");
        }

        /// <summary>
        /// Save用リクエストを生成・送信（fullUpdate用）（正常）
        /// リクエスト、レスポンスの情報が正しいこと
        /// </summary>
        [Test]
        public async void TestSendRequestForSaveNormalFullUpdate()
        {
            SaveInstallationToStorage(true);
            installation.LoadFromStorage();
            var installationId = installation.InstallationId;

            var responseBody = CreateResponseBody(true);
            var response = new MockRestResponse(HttpStatusCode.OK, responseBody.ToString());
            executor.AddResponse(response);

            // Main
            var result = await installation.SendRequestForSave();

            // Check Response
            Assert.AreEqual(result, responseBody);

            // Check Request
            var req = executor.LastRequest;
            var reqJson = NbJsonParser.Parse(req.Content.ReadAsStringAsync().Result);
            NbJsonObject reqFullUpdateJson = (NbJsonObject)reqJson["$full_update"];
            Assert.AreEqual(HttpMethod.Put, req.Method);
            Assert.IsTrue(req.Uri.EndsWith("/installations/" + installationId));
            Assert.AreEqual(3, req.Headers.Count);
            Assert.IsTrue(req.Headers.ContainsKey(appKey));
            Assert.IsTrue(req.Headers.ContainsKey(appId));
            Assert.AreEqual(reqFullUpdateJson["_channels"], installation.Channels);
            Assert.AreEqual(reqFullUpdateJson["_allowedSenders"], installation.AllowedSenders);
            Assert.AreEqual(reqFullUpdateJson["_osType"], installation.OsType);
            Assert.AreEqual(reqFullUpdateJson["_osVersion"], installation.OsVersion);
            Assert.AreEqual(reqFullUpdateJson["_deviceToken"], installation.DeviceToken);
            Assert.AreEqual(reqFullUpdateJson["_appVersionCode"], installation.AppVersionCode);
            // NUnitの場合は"0"が設定される
            Assert.AreEqual(reqFullUpdateJson["_appVersionString"], "0");
        }

        /// <summary>
        /// Save用リクエストを生成・送信（異常）
        /// NbHttpExceptionが発行されること
        /// </summary>
        [Test]
        public async void TestSendRequestForSaveExceptionFailer()
        {
            SaveInstallationToStorage(false);
            installation.LoadFromStorage();

            var response = new MockRestResponse(HttpStatusCode.Forbidden);
            executor.AddResponse(response);

            try
            {
                // Main
                var result = await installation.SendRequestForSave();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.Forbidden);
            }
        }

        /// <summary>
        /// Save用リクエストを生成・送信（fullUpdate用）（異常）
        /// NbHttpExceptionが発行されること
        /// </summary>
        [Test]
        public async void TestSendRequestForSaveExceptionFailerFullUpdate()
        {
            SaveInstallationToStorage(true);
            installation.LoadFromStorage();

            var response = new MockRestResponse(HttpStatusCode.Forbidden);
            executor.AddResponse(response);

            try
            {
                // Main
                var result = await installation.SendRequestForSave();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.Forbidden);
            }
        }

        /**
        * SendRequestForRefresh
        **/

        /// <summary>
        /// Refresh用リクエストを生成・送信（正常）
        /// リクエスト、レスポンスの情報が正しいこと
        /// </summary>
        [Test]
        public async void TestSendRequestForRefreshNormal()
        {
            SaveInstallationToStorage(true);
            installation.LoadFromStorage();
            var installationId = installation.InstallationId;

            var responseBody = CreateResponseBody(true);
            var response = new MockRestResponse(HttpStatusCode.OK, responseBody.ToString());
            executor.AddResponse(response);

            // Main
            var result = await installation.SendRequestForRefresh();

            // Check Response
            Assert.AreEqual(result, responseBody);

            // Check Request
            var req = executor.LastRequest;
            Assert.AreEqual(HttpMethod.Get, req.Method);
            Assert.IsTrue(req.Uri.EndsWith("/installations/" + installationId));
            Assert.AreEqual(3, req.Headers.Count);
            Assert.IsTrue(req.Headers.ContainsKey(appKey));
            Assert.IsTrue(req.Headers.ContainsKey(appId));
        }

        /// <summary>
        /// Refresh用リクエストを生成・送信（異常）
        /// NbHttpExceptionが発行されること
        /// </summary>
        [Test]
        public async void TestSendRequestForRefreshExceptionFailer()
        {
            SaveInstallationToStorage(true);
            installation.LoadFromStorage();

            var response = new MockRestResponse(HttpStatusCode.Forbidden);
            executor.AddResponse(response);

            try
            {
                // Main
                var result = await installation.SendRequestForRefresh();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.Forbidden);
            }
        } 
        
        /// <summary>
        /// インスタレーション情報を作成してストレージに保存する
        /// </summary>
        /// <param name="flag">インスタレーションIDを付与する場合はtrueを指定する</param>
        private void SaveInstallationToStorage(bool flag)
        {
            var json = new NbJsonObject();
            json = CreateResponseBody(flag);

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

            return body;
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

        // インスタレーションが削除されたかどうかチェック
        private void CheckDeleteInstallation(NbPushInstallationBase installation)
        {
            bool isDeleted = true;

            if (installation.OsType != null) isDeleted = false;
            if (installation.OsVersion != null) isDeleted = false;
            if (installation.DeviceToken != null) isDeleted = false;
            if (installation.PushType != null) isDeleted = false;
            if (installation.Channels != null) isDeleted = false;
            if (installation.AppVersionCode != -1) isDeleted = false;
            if (installation.AppVersionString != null) isDeleted = false;
            if (installation.AllowedSenders != null) isDeleted = false;
            if (installation.InstallationId != null) isDeleted = false;
            if (installation.Owner != null) isDeleted = false;
            if (installation.Options != null) isDeleted = false;

            if (!isDeleted)
            {
                Assert.Fail("Installation Not Deleted");
            }
        }

        // ストレージ内情報を削除
        private void DeleteStorage()
        {
            System.IO.File.Delete(InstallationFilename);
        }
    }
}
