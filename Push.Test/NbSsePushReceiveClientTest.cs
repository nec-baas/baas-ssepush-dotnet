using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nec.Nebula.Test
{
    [TestFixture]
    public class NbSsePushReceiveClientTest
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

            // インスタレーションをストレージに保存
            SaveInstallationToStorage(true, true);

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
         * Constructor(NbSsePushReceiveClient)
         **/

        /// <summary>
        /// コンストラクタ（正常）
        /// SSE情報が設定されること
        /// SsePushClientを作成すること
        /// 接続状態が切断状態となること
        /// </summary>
        [Test]
        public void TestConstructorNormal()
        {
            // Main
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            // Check State
            Assert.AreEqual(client._clientState, NbSsePushReceiveClient.State.Idle);
        }

        /**
         * AcquireLock
         **/

        /// <summary>
        /// インスタレーションの更新権利を取得
        /// Exceptionが起きないこと
        /// </summary>
        [Test]
        public void TestAcquireLockNormal()
        {
            // Main
            try
            {
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }
            finally
            {
                // 後始末
                NbSsePushReceiveClient.ReleaseLock();
            }
        }

        /// <summary>
        /// インスタレーションの更新権利を取得(複数回)
        /// 同スレッドで2回実行すると、Exxceptionが起きること
        /// </summary>
        [Test]
        public void TestAcquireLockException2Times()
        {
            // Main
            try
            {   // 1回目
                NbSsePushReceiveClient.AcquireLock();
                // 2回目
                NbSsePushReceiveClient.AcquireLock();

                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException)
            {
                // OK
            }
            finally
            {
                // 後始末
                NbSsePushReceiveClient.ReleaseLock();
            }
        }

        /// <summary>
        /// インスタレーションの更新権利を取得(複数回)
        /// AcquireLockを実行し、他スレッドで再度AcquireLockすると、Exceptionが起きること
        /// </summary>
        [Test]
        public void TestAcquireLockException2TimesFromSubThread()
        {
            try
            {   // ロック1回目
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }

            try
            {
                Task.Run(() =>
                {
                    // ロック2回目(他スレッド)
                    NbSsePushReceiveClient.AcquireLock();
                }).Wait();
                Assert.Fail("No Exception");
            }
            catch (AggregateException ae)
            {
                ae.Handle((x) =>
                {
                    if (x is InvalidOperationException)
                    {
                        // OK
                        return true;
                    }
                    else
                    {
                        Assert.Fail("Invalid Exception");
                        return false;
                    }
                });
            }
            finally
            {
                NbSsePushReceiveClient.ReleaseLock();
            }
        }

        /// <summary>
        /// インスタレーションの更新権利を取得
        /// 1回AcquireLock、1回ReleaseLockし、他スレッドでAcquireLockした場合、Exceptionが起きないこと
        /// </summary>
        [Test]
        public void TestAcquireLockNormalAfterReleaseLockInOtherThread()
        {
            // Main
            try
            {   // ロック1回目
                NbSsePushReceiveClient.AcquireLock();
                // 解除1回
                NbSsePushReceiveClient.ReleaseLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }

            try
            {
                Task.Run(() =>
                {
                    NbSsePushReceiveClient.AcquireLock();
                }).Wait();
            }
            catch (AggregateException)
            {
                Assert.Fail("Exception");
            }
            finally
            {
                NbSsePushReceiveClient.ReleaseLock();
            }
        }

        /// <summary>
        /// インスタレーションの更新権利を取得
        /// 他のスレッドで実行した場合、Exceptionが起きないこと
        /// </summary>
        [Test]
        public void TestAcquireLockNormalOtherThread()
        {
            try
            {
                Task.Run(() =>
                {
                    NbSsePushReceiveClient.AcquireLock();
                }).Wait();
            }
            catch (AggregateException)
            {
                Assert.Fail("Exception");
            }
            finally
            {
                NbSsePushReceiveClient.ReleaseLock();
            }
        }


        /**
         * ReleaseLock
         **/

        /// <summary>
        /// インスタレーションの更新権利を破棄
        /// Exceptionが起きないこと
        /// </summary>
        [Test]
        public void TestReleaseLockNormal()
        {
            NbSsePushReceiveClient.AcquireLock();

            // Main
            NbSsePushReceiveClient.ReleaseLock();

            try
            {
                // AcquireLock()でExceptionが起きないことを確認
                Task.Run(() =>
                {
                    NbSsePushReceiveClient.AcquireLock();
                    NbSsePushReceiveClient.ReleaseLock();
                }).Wait();
            }
            catch (AggregateException)
            {
                Assert.Fail("Exception");
            }
        }

        /// <summary>
        /// インスタレーションの更新権利を破棄
        /// AcquireLock()を実行しないでReleaseLock()を実行した場合、Exceptionが起きないこと
        /// </summary>
        [Test]
        public void TestReleaseLockNormalWithoutAcquire()
        {
            try
            {
                // Main
                NbSsePushReceiveClient.ReleaseLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }
        }

        /// <summary>
        /// インスタレーションの更新権利を破棄
        /// 他のスレッドでReleaseLock()を実行しても、開放されること
        /// </summary>
        [Test]
        public void TestReleaseLockNormalOtherThread()
        {
            NbSsePushReceiveClient.AcquireLock();

            // Main
            Task.Run(() =>
            {
                NbSsePushReceiveClient.ReleaseLock();
            }).Wait();

            try
            {
                // ロックできることを確認
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (InvalidOperationException)
            {
                Assert.Fail("Exception");
            }
            finally
            {
                NbSsePushReceiveClient.ReleaseLock();
            }
        }

        /**
         * Connect
         */

        /// <summary>
        /// SSEサーバと接続
        /// 接続状態が接続中となること
        /// (SsePushClient.Open()が実行されたかどうかは、Mock化できないためITで確認する)
        /// </summary>
        [Test]
        public void TestConnectNormal()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            // Main
            client.Connect();

            // Check State
            Assert.AreEqual(client._clientState, NbSsePushReceiveClient.State.Connect);
        }

        /// <summary>
        /// SSEサーバと接続(異常)
        /// 接続状態が切断状態ではない場合、NbHttpExceptionが発行されること
        /// </summary>
        [Test]
        public void TestConnectExceptionFailer()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            // 接続状態を「接続中」に設定
            client._clientState = NbSsePushReceiveClient.State.Connect;
            
            // Main
            try
            {
                client.Connect();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.BadRequest);
            }
        }

        /// <summary>
        /// SSEサーバと接続(異常)
        /// インスタレーションが存在しない場合、InvalidOperationExceptionが発行されること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void TestConnecExceptionNoId()
        {
            DeleteStorage();
            // インスタレーションIDなしでストレージに保存
            SaveInstallationToStorage(false, true);

            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            // Main

            client.Connect();
            Assert.Fail("No Exception");
        }

        /// <summary>
        /// SSEサーバと接続(異常)
        /// インスタレーション内にSSE情報が存在しない場合、NbHttpExceptionが発行されること
        /// </summary>
        [Test]
        public void TestConnecExceptionNoSseInfo()
        {
            DeleteStorage();
            SaveInstallationToStorage(true, false);

            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            // Main
            try
            {
                client.Connect();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.BadRequest);
            }
        }

        /**
        * Disconnect
        */

        /// <summary>
        /// SSEサーバと切断
        /// 接続状態が切断状態となること
        /// (SsePushClient.Close()が実行されたかどうかは、Mock化できないためITで確認する)
        /// </summary>
        [Test]
        public void TestDisconnectNormal()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            // 接続
            client.Connect();

            // Check State
            Assert.AreEqual(client._clientState, NbSsePushReceiveClient.State.Connect);

            // Main
            client.Disconnect();

            // Check State
            Assert.AreEqual(client._clientState, NbSsePushReceiveClient.State.Idle);
        }

        /**
         * AutoRecovery
         */

        /// <summary>
        /// 自動回復処理
        /// リクエストの情報が正しいこと
        /// ロックが解除されていること
        /// (SsePushClient.Open()が実行されたかどうかは、Mock化できないためITで確認する)
        /// </summary>
        [Test]
        public void TestAutoRecoveryNormal()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            var res = new Mock<HttpWebResponse>();
            var responseBody = CreateBody(true, true);
            var response = new MockRestResponse(HttpStatusCode.OK, responseBody.ToString());
            executor.AddResponse(response);

            // Main
            client.AutoRecovery(HttpStatusCode.Unauthorized, res.Object);

            // Check Request
            CheckRequest();

            // ストレージ内のインスタレーション情報存在チェック
            CheckSaveStorage();

            // Check ReleaseLock
            try
            {
                // AcquireLock()でExceptionが起きないことを確認
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }

            NbSsePushReceiveClient.ReleaseLock();
        }

        /// <summary>
        /// 自動回復処理(異常)
        /// AcquireLock()に失敗した場合、falseを返すこと
        /// </summary>
        [Test]
        public void TestAutoRecoveryExceptionFailLock()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            var res = new Mock<HttpWebResponse>();

            // ロック状態にする
            NbSsePushReceiveClient.AcquireLock();

            // Main
            Assert.IsFalse(client.AutoRecovery(HttpStatusCode.Unauthorized, res.Object));

            NbSsePushReceiveClient.ReleaseLock();
        }

        /// <summary>
        /// 自動回復処理(異常)
        /// インスタレーションIDが存在しない場合、ロックを解除し、falseを返すこと
        /// </summary>
        [Test]
        public void TestReleaseLockExceptionNoInstallationId()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            DeleteStorage();
            // インスタレーションIDなしでストレージに保存
            SaveInstallationToStorage(false, true);

            var res = new Mock<HttpWebResponse>();

            // Main
            client.AutoRecovery(HttpStatusCode.Unauthorized, res.Object);
            Assert.IsFalse(client.AutoRecovery(HttpStatusCode.Unauthorized, res.Object));

            // Check ReleaseLock
            try
            {
                // AcquireLock()でExceptionが起きないことを確認
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }

            NbSsePushReceiveClient.ReleaseLock();
        }

        /// <summary>
        /// 自動回復処理(異常)
        /// SSE情報が存在しない場合、ロックを解除し、falseを返すこと
        /// </summary>
        [Test]
        public void TestReleaseLockExceptionNoSseInfo()
        {
            DeleteStorage();
            // SSE情報なしでストレージに保存
            SaveInstallationToStorage(true, false);

            var res = new Mock<HttpWebResponse>();

            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            // Main
            Assert.IsFalse(client.AutoRecovery(HttpStatusCode.Unauthorized, res.Object));

            // Check ReleaseLock
            try
            {
                // AcquireLock()でExceptionが起きないことを確認
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }

            NbSsePushReceiveClient.ReleaseLock();
        }

        /// <summary>
        /// メッセージ受信コールバック登録(異常)
        /// イベントタイプにnullを指定した場合、InvalidOperationExceptionエラーが返ること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void TestRegisterOnMessageExceptionNullEventType()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            client.RegisterOnMessage(null, msg => { });
        }

        /// <summary>
        /// メッセージ受信コールバック登録(異常)
        /// コールバックにnullを指定した場合、InvalidOperationExceptionエラーが返ること
        /// </summary>
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void TestRegisterOnMessageExceptionNullCallback()
        {
            NbSsePushReceiveClient client = new NbSsePushReceiveClient();

            client.RegisterOnMessage("message", null);
        }

        /// <summary>
        /// RegisterOnMessage(SSE Pushサーバからメッセージ受信時の処理を登録)(イベント名指定)
        /// </summary>
        /// SsePushClient.RegisterEventをMock化できないため、ITにてテストする

        /// <summary>
        /// RegisterOnMessage(SSE Pushサーバからメッセージ受信時の処理を登録)(イベント名未指定)
        /// </summary>
        /// SsePushClient.RegisterEventをMock化できないため、ITにてテストする

        /// <summary>
        /// RegisterOnError(SSE Pushサーバからエラー受信時の処理を登録)
        /// </summary>
        /// SsePushClient.RegisterOnError()をMock化できないため、ITにてテストする
        
        /// <summary>
        /// RegisterOnOpen(SSE Pushサーバから接続完了通知受信時の処理を登録)
        /// </summary>
        /// SsePushClient.RegisterOnOpenをMock化できないため、ITにてテストする
        
        /// <summary>
        /// RegisterOnClose(SSE Pushサーバから切断完了通知受信時の処理を登録)
        /// </summary>
        /// SsePushClient.RegisterOnCloseをMock化できないため、ITにてテストする

        // ストレージ内情報を削除
        private void DeleteStorage()
        {
            System.IO.File.Delete(InstallationFilename);
            NbSsePushInstallation._sInstance = null;
        }

        /// <summary>
        /// インスタレーション情報を作成してストレージに保存する
        /// </summary>
        /// <param name="installationFlag">インスタレーションIDを付与する場合はtrueを指定する</param>
        /// <param name="sseFlag">SSE情報を付与する場合はtrueを指定する</param>
        private void SaveInstallationToStorage(bool installationFlag, bool sseFlag)
        {
            var json = CreateBody(installationFlag, sseFlag);

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

        /// <summary>
        /// ストレージ保存用のNbJsonObjectを作成する
        /// </summary>
        /// <param name="installationFlag">インスタレーションIDの付与フラグ</param>
        /// <param name="sseFlag">SSE情報の付与フラグ</param>
        /// <returns></returns>
        private NbJsonObject CreateBody(bool installationFlag, bool sseFlag)
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
            if (installationFlag)
            {
                body["_id"] = "12345";
            }
            body["_owner"] = "ownerString";

            // SSE情報付加
            NbJsonObject sse = new NbJsonObject();
            if (sseFlag)
            {
                sse["username"] = "testname";
                sse["password"] = "testpass";
            }
            // テストの都合上、URIだけは入れておく
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

        private void FindOptions(string key, NbJsonObject json, NbJsonObject option)
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

        private void CheckRequest()
        {
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan0");
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:group1");

            var req = executor.LastRequest;
            var reqJson = NbJsonParser.Parse(req.Content.ReadAsStringAsync().Result);
            NbJsonObject reqFullUpdateJson = (NbJsonObject)reqJson["$full_update"];
            Assert.AreEqual(HttpMethod.Put, req.Method);
            Assert.IsTrue(req.Uri.EndsWith("/installations/" + "12345"));
            Assert.AreEqual(3, req.Headers.Count);
            Assert.IsTrue(req.Headers.ContainsKey(appKey));
            Assert.IsTrue(req.Headers.ContainsKey(appId));
            Assert.AreEqual(reqFullUpdateJson["_osType"], "dotnet");
            Assert.AreEqual(reqFullUpdateJson["_osVersion"], "Unknown");
            Assert.AreEqual(reqFullUpdateJson["_deviceToken"], "abcdefg");
            Assert.AreEqual(reqFullUpdateJson["_pushType"], "sse");
            Assert.AreEqual(reqFullUpdateJson["_channels"], channels);
            Assert.AreEqual(reqFullUpdateJson["_appVersionCode"], -1);
            // NUnitの場合は"0"が設定される
            Assert.AreEqual(reqFullUpdateJson["_appVersionString"], "0");
            Assert.AreEqual(reqFullUpdateJson["_allowedSenders"], allowedSenders);
            Assert.AreEqual(reqFullUpdateJson["option1"], "option1value");
            Assert.AreEqual(reqFullUpdateJson["option2"], "option2value");
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
    }
}
