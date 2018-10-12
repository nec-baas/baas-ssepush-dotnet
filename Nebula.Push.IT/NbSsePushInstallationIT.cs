using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;

namespace Nec.Nebula.IT
{
    [TestFixture]
    class NbSsePushInstallationIT
    {
        [SetUp]
        public void SetUp()
        {
            ITUtil.DeleteInstallationsOfAllApp();

            ITUtil.InitNebula();

            ITUtil.DeleteAllUsers();
            ITUtil.DeleteAllGroups();

            if (NbUser.IsLoggedIn())
            {
                NbUser.LogoutAsync().Wait();
            }

            // 端末ストレージ上のインスタレーション情報削除
            ITUtil.DeleteStorage();

            // 各フィールド初期化
            NbSsePushInstallation._sInstance = null;
        }

        /// <summary>
        /// インスタレーションの新規登録 (任意の情報あり)　-ログイン済
        /// 正しく登録すること
        /// </summary>
        /// NB-.NET-PUSH-2
        /// NB-.NET-PUSH-2.1
        /// NB-.NET-PUSH-2.1.1
        /// NB-.NET-PUSH-2.1.2
        /// NB-.NET-PUSH-2.1.3
        /// NB-.NET-PUSH-2.1.3.1
        /// NB-.NET-PUSH-2.1.4
        /// NB-.NET-PUSH-2.1.5
        /// NB-.NET-PUSH-2.1.6
        /// NB-.NET-PUSH-2.1.7
        /// NB-.NET-PUSH-2.1.8
        /// NB-.NET-PUSH-2.1.9
        /// NB-.NET-PUSH-2.2
        /// NB-.NET-PUSH-2.2.1
        /// NB-.NET-PUSH-2.2.2
        /// NB-.NET-PUSH-2.2.3
        /// NB-.NET-PUSH-6.3.1
        [Test]
        public async void TestSaveInstallationNormalLoggedIn()
        {
            // SignUp & Login
            NbUser user = await ITUtil.SignUpAndLogin();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan1");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:authenticated");
            currentInstallation.AllowedSenders = allowedSenders;

            // Options Set
            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest@pushtest.com");
            options.Add("test", "testValue");
            currentInstallation.Options = options;

            // Main
            var result = await currentInstallation.Save();

            // Check Response
            ITUtil.CheckCommonResponse(result);
            Assert.AreEqual(channels, result.Channels);
            Assert.AreEqual(allowedSenders, result.AllowedSenders);
            Assert.AreEqual(user.UserId, result.Owner);
            Assert.AreEqual(options, result.Options);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの新規登録 (任意の情報あり)　-未ログイン
        /// 正しく登録すること
        /// </summary>
        /// 
        [Test]
        public async void TestSaveInstallationNormalNotLoggedIn()
        {
            // Not SignUp & Login

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan1");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:authenticated");
            currentInstallation.AllowedSenders = allowedSenders;

            // Options Set
            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest@pushtest.com");
            options.Add("test", "testValue");
            currentInstallation.Options = options;

            // Main
            var result = await currentInstallation.Save();

            // Check Response
            ITUtil.CheckCommonResponse(result);
            Assert.AreEqual(channels, result.Channels);
            Assert.AreEqual(allowedSenders, result.AllowedSenders);

            // Check Owner is null
            Assert.IsNull(result.Owner);
            Assert.AreEqual(options, result.Options);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの新規登録 (任意の情報なし)　-未ログイン
        /// 正しく登録すること
        /// </summary>
        /// NB-.NET-PUSH-2.1.9.1
        [Test]
        public async void TestSaveInstallationNormalNoOptions()
        {
            // Not SignUp & Login

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan1");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:authenticated");
            currentInstallation.AllowedSenders = allowedSenders;

            // Main
            var result = await currentInstallation.Save();

            // Check Response
            ITUtil.CheckCommonResponse(result);
            Assert.AreEqual(channels, result.Channels);
            Assert.AreEqual(allowedSenders, result.AllowedSenders);

            // Check Owner is null
            Assert.IsNull(result.Owner);

            // Check Options is null
            Assert.IsNull(result.Options);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの登録　- DeviceToken & PushType 重複
        /// 正しく登録すること
        /// </summary>
        /// NB-.NET-PUSH-2.2.4
        [Test]
        public async void TestSaveInstallationNormalSameDeviceToken()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();
            var preInstallationId = currentInstallation.InstallationId;
            var preDeviceToken = currentInstallation.DeviceToken;

            // Set InstallationId null
            currentInstallation.InstallationId = null;
            
            // Change Channels, AllowedSenders and Options

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan2");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:anonymous");
            currentInstallation.AllowedSenders = allowedSenders;

            // Options Set
            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest2@pushtest.com");
            options.Add("test", "testValue2");
            currentInstallation.Options = options;

            // Main
            var result = await currentInstallation.Save();

            // Check Response
            ITUtil.CheckCommonResponse(result);
            Assert.AreEqual(preInstallationId, result.InstallationId);
            Assert.AreEqual(channels, result.Channels);
            Assert.AreEqual(allowedSenders, result.AllowedSenders);
            Assert.AreEqual(preDeviceToken, result.DeviceToken);
            // Check Owner is null
            Assert.IsNull(result.Owner);
            Assert.AreEqual(options, result.Options);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの登録　- 必須パラメータ(Channels)なし
        /// InvalidOperationExceptionエラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-2.1.5.1
        [Test]
        public async void TestSaveInstallationExceptionNoChannels()
        {
            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Channels Not Set
            Assert.IsNull(currentInstallation.Channels);

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:authenticated");
            currentInstallation.AllowedSenders = allowedSenders;

            // Main
            try
            {
                await currentInstallation.Save();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Null Channels or null AllowedSenders.", e.Message);
            }
        }

        /// <summary>
        /// インスタレーションの登録　- 必須パラメータ(AllowedSenders)なし
        /// InvalidOperationExceptionエラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-2.1.8.1
        [Test]
        public async void TestSaveInstallationExceptionNoAllowedSenders()
        {
            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan1");
            currentInstallation.Channels = channels;

            // AllowedSenders Not Set
            Assert.IsNull(currentInstallation.AllowedSenders);

            // Main
            try
            {
                await currentInstallation.Save();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Null Channels or null AllowedSenders.", e.Message);
            }
        }

        /// <summary>
        /// インスタレーションの登録　- 必須パラメータ(DeviceToken)なし 
        /// InvalidOperationExceptionエラーが返ること
        /// </summary>
        [Test]
        public async void TestSaveInstallationExceptionNoDeviceToken()
        {
            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan1");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:authenticated");
            currentInstallation.AllowedSenders = allowedSenders;

            // Options Set
            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest@pushtest.com");
            options.Add("test", "testValue");
            currentInstallation.Options = options;

            // Set DeviceToken Null
            currentInstallation.DeviceToken = null;

            // Main
            try
            {
                await currentInstallation.Save();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Null DeviceToken.", e.Message);
            }
        }

        /// <summary>
        /// インスタレーションの登録　- テナントID誤り
        /// 404 NotFoundエラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-2.3.1
        [Test]
        public async void TestSaveInstallationExceptionInvalidTenantId()
        {
            var service = NbService.Singleton;
            service.TenantId = ("InvalidTenantId");

            // Main
            try
            {
                await ITUtil.UpsertInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.TenantId;
            }
        }

        /// <summary>
        /// インスタレーションの登録　- AppID誤り
        /// 401 UnAuthorizedエラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-2.3.1
        /// NB-.NET-PUSH-2.3.2
        [Test]
        public async void TestSaveInstallationExceptionInvalidAppId()
        {
            var service = NbService.Singleton;
            service.AppId = ("");

            // Main
            try
            {
                await ITUtil.UpsertInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
            }
            finally
            {
                // After Test
                service.AppId = TestConfig.AppId;
            }
        }

        /// <summary>
        /// インスタレーションの登録　- AppKey誤り
        /// 401 UnAuthorizedエラーが返ること
        /// </summary>
        [Test]
        public async void TestSaveInstallationExceptionInvalidAppKey()
        {
            var service = NbService.Singleton;
            service.AppKey = ("");

            // Main
            try
            {
                await ITUtil.UpsertInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
            }
            finally
            {
                // After Test
                service.AppKey = TestConfig.AppKey;
            }
        }

        /// <summary>
        /// インスタレーションの更新　- ログイン済
        /// 正しく更新すること
        /// </summary>
        /// NB-.NET-PUSH-3
        /// NB-.NET-PUSH-3.1
        /// NB-.NET-PUSH-3.1.1
        /// NB-.NET-PUSH-3.2
        /// NB-.NET-PUSH-6.3.2
        [Test]
        public async void TestUpdateInstallationNormalLoggedIn()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            Assert.IsNull(currentInstallation.Owner);
            var preInstallationId = currentInstallation.InstallationId;
            var preUsername = currentInstallation.Username;
            var prePassword = currentInstallation.Password;
            var preUri = currentInstallation.Uri;

            // Signup & Login
            NbUser user = await ITUtil.SignUpAndLogin();

            // Change Channels, AllowedSenders and Options

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan2");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:anonymous");
            currentInstallation.AllowedSenders = allowedSenders;

            // Options Set
            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest2@pushtest.com");
            options.Add("test", "testValue2");
            currentInstallation.Options = options;

            // Main
            var result = await currentInstallation.Save();

            // Check Response
            ITUtil.CheckCommonResponse(result);
            Assert.AreEqual(preInstallationId, result.InstallationId);
            Assert.AreEqual(channels, result.Channels);
            Assert.AreEqual(allowedSenders, result.AllowedSenders);
            Assert.AreEqual(options, result.Options);
            Assert.AreEqual(user.UserId, result.Owner);
            Assert.AreEqual(preUsername, result.Username);
            Assert.AreEqual(prePassword, result.Password);
            Assert.AreEqual(preUri, result.Uri);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの更新　- 未ログイン
        /// 正しく更新すること
        /// </summary>
        [Test]
        public async void TestUpdateInstallationNormalNotLoggedIn()
        {
            // Signup & Login
            var user = await ITUtil.SignUpAndLogin();

            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();
            Assert.AreEqual(user.UserId, currentInstallation.Owner);

            var preInstallationId = currentInstallation.InstallationId;
            var preUsername = currentInstallation.Username;
            var prePassword = currentInstallation.Password;
            var preUri = currentInstallation.Uri;

            // Logout
            await ITUtil.Logout();

            // Change Channels, AllowedSenders and Options
            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan2");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:anonymous");
            currentInstallation.AllowedSenders = allowedSenders;

            // Options Set
            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest2@pushtest.com");
            options.Add("test", "testValue2");
            currentInstallation.Options = options;

            // Main
            var result = await currentInstallation.Save();

            // Check Response
            ITUtil.CheckCommonResponse(result);
            Assert.AreEqual(preInstallationId, result.InstallationId);
            Assert.AreEqual(channels, result.Channels);
            Assert.AreEqual(allowedSenders, result.AllowedSenders);
            Assert.AreEqual(options, result.Options);
            // Check Owner is null
            Assert.IsNull(result.Owner);
            Assert.AreEqual(preUsername, result.Username);
            Assert.AreEqual(prePassword, result.Password);
            Assert.AreEqual(preUri, result.Uri);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの更新　- インスタレーションが存在しない
        /// 404 NotFound エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-3.3.1
        /// NB-.NET-PUSH-3.3.2
        /// NB-.NET-PUSH-6.4.2
        [Test]
        public async void TestUpdateInstallationExceptionInstallationNotExists()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Change InstallationId
            currentInstallation.InstallationId = "InvalidId";

            // Main
            try
            {
                // Update Installation
                await ITUtil.UpsertInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);

                // Check Delete Storage
                CheckDeleteStorage();
            }
        }

        /// <summary>
        /// インスタレーション情報を格納するファイルが削除されたかどうかチェック
        /// </summary>
        private void CheckDeleteStorage()
        {
            try
            {
                System.IO.File.ReadAllText(ITUtil.InstallationFilename);
            }
            catch (System.IO.FileNotFoundException)
            {
                return;
            }
            Assert.Fail("Storage Not Deleted");
        }

        /// <summary>
        /// インスタレーションの更新　- テナントID誤り
        /// 404 NotFound エラーが返ること
        /// </summary>
        [Test]
        public async void TestUpdateInstallationExceptionInvalidTenantId()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            var service = NbService.Singleton;
            service.TenantId = ("InvalidTenantId");

            // Main
            try
            {
                // Update Installation
                await ITUtil.UpsertInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);

                // Check Delete Storage
                CheckDeleteStorage();
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.TenantId;
            }
        }

        /// <summary>
        /// インスタレーションの更新　- AppID誤り
        /// 401 UnAuthorized エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-3.3.1
        /// NB-.NET-PUSH-3.3.2
        [Test]
        public async void TestUpdateInstallationExceptionInvalidAppId()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            var service = NbService.Singleton;
            service.AppId = ("InvalidAppId");

            // Main
            try
            {
                // Update Installation
                await ITUtil.UpsertInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
            }
            finally
            {
                // After Test
                service.AppId = TestConfig.AppId;
            }
        }

        /// <summary>
        /// インスタレーションの取得　- インスタレーション登録済
        /// 正しく取得すること
        /// </summary>
        /// NB-.NET-PUSH-5.1
        /// NB-.NET-PUSH-6.3.3
        [Test]
        public async void TestRefreshInstallationNormalSaved()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();
            var channels = currentInstallation.Channels;
            var allowedSenders = currentInstallation.AllowedSenders;
            var owner = currentInstallation.Owner;
            var options = currentInstallation.Options;

            // Change Strorage Data
            var json = ITUtil.GetJsonFromStorage();
            json["options"] = null;
            ITUtil.SaveJsonToStorage(json);

            // Main
            var result = await NbSsePushInstallation.RefreshCurrentInstallation();

            // Check Response
            ITUtil.CheckCommonResponse(result);
            Assert.AreEqual(channels, result.Channels);
            Assert.AreEqual(allowedSenders, result.AllowedSenders);
            Assert.AreEqual(owner, result.Owner);
            Assert.AreEqual(options, result.Options);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの取得　- インスタレーション未登録
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-5.3
        [Test]
        public async void TestRefreshInstallationExceptionNotSaved()
        {
            // Main
            try
            {
                await NbSsePushInstallation.RefreshCurrentInstallation();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Null installationId", e.Message);
            }
        }

        /// <summary>
        /// インスタレーションの取得　- インスタレーションが存在しない
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-5.2.1
        /// NB-.NET-PUSH-5.2.2
        /// NB-.NET-PUSH-6.4.2
        [Test]
        public async void TestRefreshInstallationExceptionNotExists()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Change InstallationId
            currentInstallation.InstallationId = "InvalidId";

            // Main
            try
            {
                await NbSsePushInstallation.RefreshCurrentInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);

                // Check Delete Storage
                CheckDeleteStorage();
            }
        }

        /// <summary>
        /// インスタレーションの取得　- テナントID誤り
        /// 404 Not Foundエラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-5.2.1
        /// NB-.NET-PUSH-5.2.2
        [Test]
        public async void TestRefreshInstallationExceptionInvalidTenantId()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            var service = NbService.Singleton;
            service.TenantId = ("InvalidTenantId");

            // Main
            try
            {
                await NbSsePushInstallation.RefreshCurrentInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.TenantId;
            }
        }

        /// <summary>
        /// インスタレーションの取得　- AppID誤り
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-5.2.1
        /// NB-.NET-PUSH-5.2.2
        [Test]
        public async void TestRefreshInstallationExceptionInvalidAppId()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            var service = NbService.Singleton;
            service.AppId = ("InvalidAppId");

            // Main
            try
            {
                await NbSsePushInstallation.RefreshCurrentInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.AppId;
            }
        }

        /// <summary>
        /// インスタレーションの取得　- AppKey誤り
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-5.2.1
        /// NB-.NET-PUSH-5.2.2
        [Test]
        public async void TestRefreshInstallationExceptionInvalidAppKey()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            var service = NbService.Singleton;
            service.AppKey = ("InvalidAppKey");

            // Main
            try
            {
                await NbSsePushInstallation.RefreshCurrentInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.AppKey;
            }
        }

        /// <summary>
        /// インスタレーションの削除　- インスタレーション登録済
        /// 正しく削除すること
        /// </summary>
        /// NB-.NET-PUSH-4
        /// NB-.NET-PUSH-6.4.1
        [Test]
        public async void TestDeleteInstallationNormal()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Main
            await currentInstallation.DeleteInstallation();

            // Check Delete Storage
            CheckDeleteStorage();
        }

        /// <summary>
        /// インスタレーションの削除　- インスタレーション未登録
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-4.2
        [Test]
        public async void TestDeleteInstallationExceptionNotSaved()
        {
            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Main
            try
            {
                await currentInstallation.DeleteInstallation();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Null installationId", e.Message);
            }
        }

        /// <summary>
        /// インスタレーションの削除　- インスタレーションが存在しない
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-4.1.1
        /// NB-.NET-PUSH-4.1.2
        /// NB-.NET-PUSH-6.4.2
        [Test]
        public async void TestDeleteInstallationExceptionNotExists()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Change InstallationId
            currentInstallation.InstallationId = "InvalidId";

            // Main
            try
            {
                await currentInstallation.DeleteInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);

                // Check Delete Storage
                CheckDeleteStorage();
            }
        }

        /// <summary>
        /// インスタレーションの削除　- テナントID誤り
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-4.1.1
        /// NB-.NET-PUSH-4.1.2
        [Test]
        public async void TestDeleteInstallationExceptionInvalidTenantId()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            var service = NbService.Singleton;
            service.TenantId = ("InvalidTenantId");

            // Main
            try
            {
                await currentInstallation.DeleteInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.TenantId;
            }
        }

        /// <summary>
        /// インスタレーションの削除　- AppIdID誤り
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-4.1.1
        /// NB-.NET-PUSH-4.1.2
        [Test]
        public async void TestDeleteInstallationExceptionInvalidAppId()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            var service = NbService.Singleton;
            service.AppId = ("InvalidAppId");

            // Main
            try
            {
                await currentInstallation.DeleteInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.AppId;
            }
        }

        /// <summary>
        /// インスタレーションの削除　- AppKey誤り
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-4.1.1
        /// NB-.NET-PUSH-4.1.2
        [Test]
        public async void TestDeleteInstallationExceptionInvalidAppKey()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            var service = NbService.Singleton;
            service.AppKey = ("InvalidAppKey");

            // Main
            try
            {
                await currentInstallation.DeleteInstallation();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.AppKey;
            }
        }

        /// <summary>
        /// インスタレーションの削除後、再登録
        /// 正しく削除・再登録できること
        /// </summary>
        /// チケット #3396「インスタレーション登録⇒削除⇒再登録時にエラー」に対するテスト
        [Test]
        public async void TestDeleteAndSaveInstallationNormal()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();
            var deviceToken = currentInstallation.DeviceToken;

            // Main
            await currentInstallation.DeleteInstallation();

            // Check Delete Storage
            CheckDeleteStorage();

            // Save(again)
            var result = await ITUtil.UpsertInstallation();

            // Check Response
            ITUtil.CheckCommonResponse(result);

            ISet<string> channels = new HashSet<string>();
            channels.Add("chan1");
            Assert.AreEqual(channels, result.Channels);

            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:anonymous");
            Assert.AreEqual(allowedSenders, result.AllowedSenders);

            // Check Owner is null
            Assert.IsNull(result.Owner);

            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest@pushtest.com");
            options.Add("test", "testValue");
            Assert.AreEqual(options, result.Options);

            // Check DeviceToken
            Assert.AreEqual(deviceToken, result.DeviceToken);

            // Check Storage
            ITUtil.CheckSaveStorage(result);
        }

        /// <summary>
        /// インスタレーションの取得(端末ストレージから)　- インスタレーション情報なし
        /// 正しく取得できること
        /// </summary>
        /// NB-.NET-PUSH-6.1
        /// NB-.NET-PUSH-6.2.1
        [Test]
        public void TestGetCurrentInstallationNormalNotSaved()
        {
            // Main
            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Assert Equal Installation
            AssertAreEqualInstallation(null, currentInstallation);
        }

        /// <summary>
        /// インスタレーションの取得(端末ストレージから)　- インスタレーション情報あり
        /// 正しく取得できること
        /// </summary>
        /// NB-.NET-PUSH-6.1
        /// NB-.NET-PUSH-6.2.1
        [Test]
        public async void TestGetCurrentInstallationNormalSaved()
        {
            // Save Installation
            var upsertInstallation = await ITUtil.UpsertInstallation();

            NbSsePushInstallation._sInstance = null;

            // Main
            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Assert Equal Installation
            AssertAreEqualInstallation(upsertInstallation, currentInstallation);
        }

        /// <summary>
        /// インスタレーションの取得(端末ストレージから)　- インスタレーション情報あり - 2回実行
        /// 正しく取得できること
        /// </summary>
        /// NB-.NET-PUSH-6.1
        /// NB-.NET-PUSH-6.2.1
        [Test]
        public async void TestGetCurrentInstallationNormalNotSavedTwice()
        {
            // Save Installation
            var upsertInstallation = await ITUtil.UpsertInstallation();

            // GetCurrentInstallation()で端末ストレージからインスタレーション取得するために、nullに設定する
            NbSsePushInstallation._sInstance = null;

            // Main
            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Assert Equal Installation
            AssertAreEqualInstallation(upsertInstallation, currentInstallation);

            currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Assert Equal Installation
            AssertAreEqualInstallation(upsertInstallation, currentInstallation);
        }

        /// <summary>
        /// インスタレーションを比較
        /// expected == null の場合は初期値と比較
        /// </summary>
        /// <param name="expected">比較対象インスタレーション 1</param>
        /// <param name="actual">比較対象インスタレーション 2</param>
        private void AssertAreEqualInstallation(NbSsePushInstallation expected, NbSsePushInstallation actual)
        {
            if (expected != null)
            {
                Assert.IsNotNull(actual);
                Assert.AreEqual(expected.OsType, actual.OsType);
                Assert.AreEqual(expected.OsVersion, actual.OsVersion);
                Assert.AreEqual(expected.DeviceToken, actual.DeviceToken);
                Assert.AreEqual(expected.PushType, actual.PushType);
                Assert.AreEqual(expected.Channels, actual.Channels);
                Assert.AreEqual(expected.AppVersionCode, actual.AppVersionCode);
                Assert.AreEqual(expected.AppVersionString, actual.AppVersionString);
                Assert.AreEqual(expected.AllowedSenders, actual.AllowedSenders);
                Assert.AreEqual(expected.Owner, actual.Owner);
                Assert.AreEqual(expected.InstallationId, actual.InstallationId);
                Assert.AreEqual(expected.Options, actual.Options);
                Assert.AreEqual(expected.Uri, actual.Uri);
                Assert.AreEqual(expected.Username, actual.Username);
                Assert.AreEqual(expected.Password, actual.Password);
            }
            else
            {
                Assert.IsNotNull(actual);
                Assert.IsNull(actual.OsType);
                Assert.IsNull(actual.OsVersion);
                Assert.IsNotNullOrEmpty(actual.DeviceToken);
                Assert.IsNull(actual.PushType);
                Assert.IsNull(actual.Channels);
                Assert.AreEqual(-1, actual.AppVersionCode);
                Assert.IsNull(actual.AppVersionString);
                Assert.IsNull(actual.AllowedSenders);
                Assert.IsNull(actual.Owner);
                Assert.IsNull(actual.InstallationId);
                Assert.IsNull(actual.Options);
                Assert.IsNull(actual.Uri);
                Assert.IsNull(actual.Username);
                Assert.IsNull(actual.Password);
            }
        }
    }
}
