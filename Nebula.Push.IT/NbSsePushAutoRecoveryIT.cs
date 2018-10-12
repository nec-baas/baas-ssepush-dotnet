using NUnit.Framework;
using System;
using System.Net;
using System.Threading;

namespace Nec.Nebula.IT
{
    [TestFixture]
    class NbSsePushAutoRecoveryIT
    {
        NbSsePushReceiveClient _client;

        // Assertメッセージ格納用
        private string _assertString;
        
        // Assertメッセージ存在確認用
        bool _isAssertionExists;

        // 各コールバックの呼び出し回数
        int _openCalledCount;
        int _closeCalledCount;
        int _errorCalledCount;
        int _messageCalledCount;

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
            _isAssertionExists = false;
            _openCalledCount = 0;
            _closeCalledCount = 0;
            _errorCalledCount = 0;
            _messageCalledCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_client != null)
            {
                NbSsePushReceiveClient.ReleaseLock();
                _client.Disconnect();
                _client._sseClient = null;
                _client = null;
            }
        }

        /// <summary>
        /// 自動再接続
        /// ExponentialBackoffで再接続を試みること
        /// </summary>
        /// NB-.NET-PUSH-7.4
        /// NB-.NET-PUSH-7.4.1.1
        /// NB-.NET-PUSH-7.4.1.2
        /// NB-.NET-PUSH-7.4.1.3
        /// 
        // 500 Errorを返す方法がないためテスト不可
        // ServerSentEventsモジュールのUTでは確認済
        // ⇒ TestEventSourceAutoConnect()
        //[Test]
        public void TestAutoConnectNormal()
        {
        }

        /// <summary>
        /// 自動回復処理
        /// 再接続されること
        /// </summary>
        /// NB-.NET-PUSH-7.3.1
        /// NB-.NET-PUSH-7.3.2.2
        /// NB-.NET-PUSH-7.3.2.3
        [Test]
        public async void TestAutoRecoveryNormal()
        {
            // For Callback Wait Class
            ManualResetEvent manualEventForOnOpen = new ManualResetEvent(false);
            ManualResetEvent manualEventForOnMessage = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();

            // Set Invalid Password
            installation.Password = "InvalidPassword";

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;
                manualEventForOnMessage.Set();
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEventForOnOpen.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Main
            _client.Connect();

            // Wait for OnOpen Callback with Timeout
            manualEventForOnOpen.WaitOne(10000);

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();

            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            await nebulaPush.SendAsync();

            // Wait for OnMessage Callback with Timeout
            manualEventForOnMessage.WaitOne(10000);
            
            // Check Callback Count

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);

            // Check Installation
            var installationAfterAutoRecovery = NbSsePushInstallation.GetCurrentInstallation();
            ITUtil.CheckCommonResponse(installationAfterAutoRecovery);
            Assert.AreEqual(installation.Channels, installationAfterAutoRecovery.Channels);
            Assert.AreEqual(installation.AllowedSenders, installationAfterAutoRecovery.AllowedSenders);
            Assert.IsNull(installationAfterAutoRecovery.Owner);
            Assert.AreEqual(installation.Options, installationAfterAutoRecovery.Options);

            // Check Storage
            ITUtil.CheckSaveStorage(installationAfterAutoRecovery);
        }

        /// <summary>
        /// 自動回復処理 - ロック中 
        /// AcquireLock()に失敗した場合、コールバックを返すこと
        /// </summary>
        [Test]
        public async void TestAutoRecoveryCallbackLocked()
        { 
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();

            // Set Invalid Password in order to execute AutoRecovery
            installation.Password = "InvalidPassword";

            _client = new NbSsePushReceiveClient();

            // Set Lock
            NbSsePushReceiveClient.AcquireLock();

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;

                if (statusCode != HttpStatusCode.Unauthorized)
                {
                    SetAssert("Not Unauthorized Error: " + statusCode.ToString());
                }
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Wait for OnError Callback with Timeout
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Error Callback: Once
            Assert.AreEqual(1, _errorCalledCount);
        }

        /// <summary>
        /// 自動回復処理 - インスタレーションが存在しない 
        /// インスタレーションが存在しない場合、コールバックを返すこと
        /// AutoRecovery実行前にinstallation.Password = nullにする
        /// </summary>
        /// NB-.NET-PUSH-7.3.2.1
        [Test]
        public async void TestAutoRecoveryCallbackInstallationNotExists()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();

            // Set Invalid Password in order to execute AutoRecovery
            installation.Password = "InvalidPassword";

            _client = new NbSsePushReceiveClient();

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;

                if (statusCode != HttpStatusCode.Unauthorized)
                {
                    SetAssert("Not Unauthorized Error: " + statusCode.ToString());
                }
                manualEvent.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                // Set Password Null Before AutoRecovery
                installation.Password = null;
                _closeCalledCount++;
            });

            // Main
            _client.Connect();

            // Wait for OnError Callback with Timeout
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Check Release Locked
            try
            {
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }

            NbSsePushReceiveClient.ReleaseLock();

            // Error Callback: Once
            Assert.AreEqual(1, _errorCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);
        }

        /// <summary>
        /// 自動回復処理 - インスタレーション再登録に失敗 
        /// インスタレーション再登録に失敗した場合、コールバックを返すこと
        /// AutoRecovery実行前にinstallation.Channels = nullにする
        /// </summary>
        [Test]
        public async void TestAutoRecoveryCallbackSaveInstallationFail()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();

            // Set Invalid Password
            installation.Password = "InvalidPassword";

            _client = new NbSsePushReceiveClient();

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;

                if (statusCode != HttpStatusCode.Unauthorized)
                {
                    SetAssert("Not Unauthorized Error: " + statusCode.ToString());
                }
                manualEvent.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                // Set Channels Null Before AutoRecovery
                installation.Channels = null;
                _closeCalledCount++;
            });

            // Main
            _client.Connect();

            // Wait for OnError Callback with Timeout
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Check Release Locked
            try
            {
                NbSsePushReceiveClient.AcquireLock();
            }
            catch (Exception)
            {
                Assert.Fail("Exception");
            }

            NbSsePushReceiveClient.ReleaseLock();

            // Error Callback: Once
            Assert.AreEqual(1, _errorCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);
        }

        /// <summary>
        /// 自動回復処理 - Errorコールバック未登録
        /// 正しく自動回復処理が実施されること
        /// </summary>
        [Test]
        public async void TestAutoRecoveryNormalNotOnErrorRegistered()
        {
            // For Callback Wait Class
            ManualResetEvent manualEventForOnOpen = new ManualResetEvent(false);
            ManualResetEvent manualEventForOnMessage = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();

            // Set Invalid Password
            installation.Password = "InvalidPassword";

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;
                manualEventForOnMessage.Set();
            });

            // Not Register Error Callback

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEventForOnOpen.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Main
            _client.Connect();

            // Wait for OnOpen Callback with Timeout
            manualEventForOnOpen.WaitOne(10000);

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();

            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            await nebulaPush.SendAsync();

            // Wait for OnMessage Callback with Timeout
            manualEventForOnMessage.WaitOne(10000);

            // Check Callback Count

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);

            // Check Installation
            var installationAfterAutoRecovery = NbSsePushInstallation.GetCurrentInstallation();
            ITUtil.CheckCommonResponse(installationAfterAutoRecovery);
            Assert.AreEqual(installation.Channels, installationAfterAutoRecovery.Channels);
            Assert.AreEqual(installation.AllowedSenders, installationAfterAutoRecovery.AllowedSenders);
            Assert.IsNull(installationAfterAutoRecovery.Owner);
            Assert.AreEqual(installation.Options, installationAfterAutoRecovery.Options);

            // Check Storage
            ITUtil.CheckSaveStorage(installationAfterAutoRecovery);
        }

        private void SetAssert(string assertString)
        {
            _isAssertionExists = true;
            _assertString = assertString;
        }

        private void ThrowAssert()
        {
            Assert.Fail(_assertString);
        }

    }
}
