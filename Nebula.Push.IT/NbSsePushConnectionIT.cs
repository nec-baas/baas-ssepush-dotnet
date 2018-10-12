using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Nec.Nebula.IT
{
    [TestFixture]
    class NbSsePushConnectionIT
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
        
        // ★ネットワーク切断・接続テスト(TestNwDisconnectAndConnect())は、
        // 　手動でネットワークを切断・接続する必要がある。
        // 　実行する場合は、コメントアウトを外して手動でテストすること。

        /// <summary>
        /// Push送受信 - queryに含まれる
        /// Push送受信ができること
        /// </summary>
        /// DOTNET-PUSH-1.2
        /// DOTNET-PUSH-1.2.1
        /// DOTNET-PUSH-1.2.1.1
        /// DOTNET-PUSH-1.2.2
        /// DOTNET-PUSH-1.2.2.1
        /// DOTNET-PUSH-1.2.3.3
        /// DOTNET-PUSH-1.2.3.4
        /// DOTNET-PUSH-1.5
        /// DOTNET-PUSH-1.6.1
        /// DOTNET-PUSH-4.1
        /// DOTNET-PUSH-4.1.1
        /// DOTNET-PUSH-4.2
        /// NB-.NET-PUSH-7.1
        /// NB-.NET-PUSH-7.2
        /// NB-.NET-PUSH-7.2.1
        /// NB-.NET-SSE-1
        /// NB-.NET-SSE-1.1
        /// NB-.NET-SSE-1.1.1
        /// NB-.NET-SSE-1.2
        /// NB-.NET-SSE-1.3
        /// NB-.NET-SSE-1.4
        /// NB-.NET-SSE-1.6
        /// NB-.NET-SSE-1.6.1
        [Test]
        public async void TestConnectNormalMatchQuery()
        {
            // For Open Callback Wait Class
            ManualResetEvent manualEventForOpen = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                try
                {
                    Assert.AreEqual("This is Test.", message.Data);
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEventForOpen.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
            });

            // Main
            _client.Connect();

            // Wait for Callback With Timeout
            manualEventForOpen.WaitOne(10000);

            Thread.Sleep(2000);

            // Send Message
            var nebulaPush = new NbPush();
            Assert.IsNull(nebulaPush.Query);
            Assert.IsNull(nebulaPush.Message);
            Assert.IsNull(nebulaPush.AllowedReceivers);

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            Assert.IsNull(sse.EventId);
            Assert.IsNull(sse.EventType);

            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
            }

            // Waiting Class For Confirming that Receive Message Once
            ManualResetEvent manualEvent = new ManualResetEvent(false);
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);
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

        /// <summary>
        /// Push送受信 - 複数イベント受信
        /// Push送受信ができること、複数のイベントをそれぞれ受信できること
        /// </summary>
        [Test]
        public async void TestConnectNormalMultiEvents()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message1 Callback
            _client.RegisterOnMessage("TestEventType1", (message) =>
            {
                _messageCalledCount++;
                try
                {
                    Assert.AreEqual("This is Test1.", message.Data);
                    Assert.AreEqual("TestEventType1", message.EventType);
                    Assert.AreEqual("TestId1", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.ToString());
                    manualEvent.Set();
                }
            });

            // Register Message2 Callback
            _client.RegisterOnMessage("TestEventType2", (message) =>
            {
                _messageCalledCount++;
                try
                {
                    Assert.AreEqual("This is Test2.", message.Data);
                    Assert.AreEqual("TestEventType2", message.EventType);
                    Assert.AreEqual("TestId2", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.ToString());
                }
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");

            // Send 2 Messages
            for (var i = 1; i < 3; i++)
            {
                var sse = new NbSseFields();
                sse.EventId = "TestId" + i;
                sse.EventType = "TestEventType" + i;

                nebulaPush.SseFields = sse;
                nebulaPush.Message = "This is Test" + i + ".";

                try
                {
                    var response = await nebulaPush.SendAsync();
                    Assert.AreEqual("ok", response["result"]);
                    Assert.AreEqual(1, response["installations"]);
                }
                catch (Exception)
                {
                    SetAssert("Send Fail");
                    manualEvent.Set();
                }
                Thread.Sleep(100);
            }

            // Wait for Callback
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Twice
            Assert.AreEqual(2, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - queryに含まれない
        /// Push送信に成功し、受信しないこと
        /// </summary>
        [Test]
        public async void TestConnectNormalUnMatchQuery()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                // Fail
                SetAssert("Received Message");
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery().EqualTo("email", "InvalidEmail@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(0, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback with Timeout
            manualEvent.WaitOne(3000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: None
            Assert.AreEqual(0, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - allowedReceiversに含まれる(ユーザID一致)
        /// Push送受信ができること
        /// </summary>
        /// DOTNET-PUSH-1.2.3
        /// DOTNET-PUSH-1.2.3.1
        [Test]
        public async void TestConnectNormalMatchUserIdInAllowedReceivers()
        {
            // For Open Callback Wait Class
            ManualResetEvent manualEventForOnOpen = new ManualResetEvent(false);

            // For Message Callback Wait Class
            ManualResetEvent manualEventForOnMessage = new ManualResetEvent(false);

            // Save Installation
            var user = await ITUtil.SignUpAndLogin();
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                try
                {
                    Assert.AreEqual("This is Test.", message.Data);
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
                manualEventForOnMessage.Set();
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

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEventForOnOpen.Set();
                manualEventForOnMessage.Set();
            });

            // Main
            _client.Connect();

            // Wait for Open Callback With Timeout
            manualEventForOnOpen.WaitOne(10000);

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery();
            nebulaPush.Message = "This is Test.";

            // Set AllowedReceivers(userId)
            ISet<string> allowedReceivers = new HashSet<string>();
            allowedReceivers.Add(user.UserId);
            nebulaPush.AllowedReceivers = allowedReceivers;

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEventForOnMessage.Set();
            }

            // Wait for Callback With Timeout
            manualEventForOnMessage.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - allowedReceiversに含まれる(グループに含まれる)
        /// Push送受信ができること
        /// </summary>
        /// 
        [Test]
        public async void TestConnectNormalMatchGroupInAllowedReceivers()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var user = await ITUtil.SignUpAndLogin();

            // Create Group
            ISet<string> users = new HashSet<string>();
            users.Add(user.UserId);
            var group = await ITUtil.CreateGroup(users);

            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                try
                {
                    Assert.AreEqual("This is Test.", message.Data);
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery();
            nebulaPush.Message = "This is Test.";

            // Set AllowedReceivers(group)
            ISet<string> allowedReceivers = new HashSet<string>();
            allowedReceivers.Add("g:" + group.Name);
            nebulaPush.AllowedReceivers = allowedReceivers;

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback With Timeout
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - allowedReceiversに含まれない
        /// Push送信に成功し、受信しないこと
        /// </summary>
        /// DOTNET-PUSH-1.2.3
        /// DOTNET-PUSH-1.2.3.1
        [Test]
        public async void TestConnectNormalUnMatchAllowedReceivers()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var user = await ITUtil.SignUpAndLogin();
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                // Fail
                SetAssert("Received Message");
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery();
            nebulaPush.Message = "This is Test.";

            // Set AllowedSenders(Not Exist UserId)
            ISet<string> allowedReceivers = new HashSet<string>();
            allowedReceivers.Add("InvalidUserId");
            nebulaPush.AllowedReceivers = allowedReceivers;

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(0, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback with Timeout
            manualEvent.WaitOne(3000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: None
            Assert.AreEqual(0, _messageCalledCount);
        }

        /// <summary>
        /// Push送信 - allowedReceiversに"g:anonymous"を指定
        /// Push送信に失敗すること(400 BadRequest Error)
        /// </summary>
        /// DOTNET-PUSH-1.2.3.2
        [Test]
        public async void TestConnectNormalInEffectiveAllowedReceivers_Anonymous()
        {
            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery();
            nebulaPush.Message = "This is Test.";

            // Set AllowedSenders(g:anonymous)
            ISet<string> allowedReceivers = new HashSet<string>();
            allowedReceivers.Add("g:anonymous");
            nebulaPush.AllowedReceivers = allowedReceivers;

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, e.StatusCode);
            }
        }

        /// <summary>
        /// Push送信 - allowedReceiversに"g:authenticated"を指定
        /// Push送信に失敗すること(400 BadRequest Error)
        /// </summary>
        /// DOTNET-PUSH-1.2.3.2
        [Test]
        public async void TestConnectNormalInEffectiveAllowedReceivers_Authenticated()
        {
            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery();
            nebulaPush.Message = "This is Test.";

            // Set AllowedSenders(g:authenticated)
            ISet<string> allowedReceivers = new HashSet<string>();
            allowedReceivers.Add("g:authenticated");
            nebulaPush.AllowedReceivers = allowedReceivers;

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, e.StatusCode);
            }
        }

        /// <summary>
        /// Push送受信 - 送信と受信のイベントタイプ不一致
        /// Push送信に成功し、受信しないこと
        /// </summary>
        /// 
        [Test]
        public async void TestConnectNormalUnMatchEventType()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                // Fail
                SetAssert("Receive Message");
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery();
            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "DifferentEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                try
                {
                    Assert.AreEqual("ok", response["result"]);
                    Assert.AreEqual(1, response["installations"]);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                    manualEvent.Set();
                }
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback with Timeout
            manualEvent.WaitOne(3000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: None
            Assert.AreEqual(0, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - 送信時にイベントタイプを指定しない
        /// Push送信に成功し、受信しないこと
        /// </summary>
        /// DOTNET-PUSH-4.2.1
        [Test]
        public async void TestConnectNormalNoEventType()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                // Fail
                SetAssert("Receive Message");
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery();
            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            // Not Set EventType
            nebulaPush.SseFields = sse;
            Assert.IsNull(sse.EventType);

            try
            {
                var response = await nebulaPush.SendAsync();
                try
                {
                    Assert.AreEqual("ok", response["result"]);
                    Assert.AreEqual(1, response["installations"]);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback with Timeout
            manualEvent.WaitOne(3000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: None
            Assert.AreEqual(0, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - 単文2回
        /// Push送受信ができること
        /// </summary>
        /// 
        [Test]
        public async void TestConnectNormalTwice()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;
                try
                {
                    switch (_messageCalledCount)
                    {
                        case 1:
                            Assert.AreEqual("This is Test1.", message.Data);
                            break;
                        case 2:
                            Assert.AreEqual("This is Test2.", message.Data);
                            manualEvent.Set();
                            break;
                        default:
                            Assert.Fail("Received too many Messages.");
                            break;
                    }
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                    manualEvent.Set();
                }
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();
            Assert.IsNull(nebulaPush.Query);
            Assert.IsNull(nebulaPush.Message);
            Assert.IsNull(nebulaPush.AllowedReceivers);

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");

            var sse = new NbSseFields();
            Assert.IsNull(sse.EventId);
            Assert.IsNull(sse.EventType);

            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            // 1回目
            try
            {
                nebulaPush.Message = "This is Test1.";
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            Thread.Sleep(300);

            // 2回目
            try
            {
                nebulaPush.Message = "This is Test2.";
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback With Timeout
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Twice
            Assert.AreEqual(2, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - 複数行2回
        /// Push送受信ができること
        /// </summary>
        [Test]
        public async void TestConnectNormalMultiLineTwice()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;
                try
                {
                    switch (_messageCalledCount)
                    {
                        case 1:
                            Assert.AreEqual("This is\nMultiTest1.", message.Data);
                            break;
                        case 2:
                            Assert.AreEqual("This is\nMultiTest2.", message.Data);
                            manualEvent.Set();
                            break;
                        default:
                            Assert.Fail("Received too many Messages.");
                            break;
                    }
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                    manualEvent.Set();
                }
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Send Message
            var nebulaPush = new NbPush();
            Assert.IsNull(nebulaPush.Query);
            Assert.IsNull(nebulaPush.Message);
            Assert.IsNull(nebulaPush.AllowedReceivers);

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");

            var sse = new NbSseFields();
            Assert.IsNull(sse.EventId);
            Assert.IsNull(sse.EventType);

            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            // 1回目
            try
            {
                nebulaPush.Message = "This is\nMultiTest1.";
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            Thread.Sleep(500);

            // 2回目
            try
            {
                nebulaPush.Message = "This is\nMultiTest2.";
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback With Timeout
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Twice
            Assert.AreEqual(2, _messageCalledCount);
        }

        /// <summary>
        /// Push送受信 - サブスレッドからの通信開始
        /// Push送受信ができること
        /// </summary>
        /// 
        [Test]
        public async void TestConnectNormalFromSubThread()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                try
                {
                    Assert.AreEqual("This is Test.", message.Data);
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
                manualEvent.Set();
            });

            // Main
            try
            {
                Task.Run(() =>
                {
                    _client.Connect();
                }).Wait();
            }
            catch (AggregateException)
            {
                SetAssert("AggregateException");
            }

            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();

            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
                manualEvent.Set();
            }

            // Wait for Callback With Timeout
            manualEvent.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);
        }

        /// <summary>
        /// Push通信開始 - 接続中の状態
        /// エラーが返ること
        /// </summary>
        [Test]
        public async void TestConnectExceptionDuringConnected()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEvent.Set();
            });

            _client.Connect();

            // Wait For Open Callback
            manualEvent.WaitOne(10000);

            Assert.AreEqual(1, _openCalledCount);

            // Main
            try
            {
                _client.Connect();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, e.StatusCode);
                Assert.AreEqual("Connection is already being processed.", e.Message);
            }
        }

        /// <summary>
        /// Push通信開始 - クライアントPush送信が許可されていない - AppKey使用
        /// クライアントPush送信が許可されていない場合、403エラーを返すこと
        /// デベロッパコンソールで「クライアントPush」を禁止にする
        /// </summary>
        /// DOTNET-PUSH-1.5.
        [Test]
        public async void TestSendExceptionClientPushForbiddenUseAppKey()
        {
            // Use App which ClientPush is forbidden
            ITUtil.InitNebulaForPushDisabled();

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Forbidden, e.StatusCode);
                Assert.IsNotNull(ITUtil.GetErrorInfo(e.Response));
            }
        }

        /// <summary>
        /// Push通信開始 - クライアントPush送信が許可されていない - MasterKey使用
        /// クライアントPush送信が許可されていない場合、マスターキーを使用して送信できること
        /// デベロッパコンソールで「クライアントPush」を禁止にする
        /// </summary>
        /// DOTNET-PUSH-1.7.
        [Test]
        public async void TestSendNormalClientPushForbiddenUseMasterKey()
        {
            // Use App which ClientPush is forbidden
            ITUtil.InitNebulaForPushDisabled();

            // Use MasterKey
            ITUtil.UseMasterKeyForPushDisabled();

            // For Open Callback Wait Class
            ManualResetEvent manualEventForOpen = new ManualResetEvent(false);

            // For Message Callback Wait Class
            ManualResetEvent manualEventForMessage = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                try
                {
                    Assert.AreEqual("This is Test.", message.Data);
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
                manualEventForMessage.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEventForOpen.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
            });

            // Main
            _client.Connect();

            // Wait for Callback With Timeout
            manualEventForOpen.WaitOne(10000);

            Thread.Sleep(2000);

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
            }

            // Wait for Callback With Timeout
            manualEventForMessage.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Once
            Assert.AreEqual(1, _openCalledCount);

            // Close Callback: None
            Assert.AreEqual(0, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);

            // After Test
            // Use AppKey
            ITUtil.UseNormalKey();
        }

        /// <summary>
        /// Push送信 - メッセージがない場合
        /// エラーが返ること
        /// </summary>
        /// DOTNET-PUSH-1.3
        [Test]
        public async void TestSendExceptionMessageNull()
        {
            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery();

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("No message", e.Message);
            }
        }

        /// <summary>
        /// Push通信開始 - ユーザ名がない場合
        /// エラーが返ること
        /// </summary>
        [Test]
        public async void TestConnectExceptionUsernameNull()
        {
            // Save Installation
            var installation = await ITUtil.UpsertInstallation();
            installation.Username = null;

            _client = new NbSsePushReceiveClient();

            // Main
            try
            {
                _client.Connect();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, e.StatusCode);
                Assert.AreEqual("SSE Infomation doesn't exist in Installation.", e.Message);
            }
        }

        /// <summary>
        /// Push通信開始 - パスワードがない場合
        /// エラーが返ること
        /// </summary>
        [Test]
        public async void TestConnectExceptionPasswordNull()
        {
            // Save Installation
            var installation = await ITUtil.UpsertInstallation();
            installation.Password = null;

            _client = new NbSsePushReceiveClient();

            // Main
            try
            {
                _client.Connect();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, e.StatusCode);
                Assert.AreEqual("SSE Infomation doesn't exist in Installation.", e.Message);
            }
        }

        /// <summary>
        /// Push通信開始 - Uriがない場合
        /// エラーが返ること
        /// </summary>
        [Test]
        public async void TestConnectExceptionUriNull()
        {
            // Save Installation
            var installation = await ITUtil.UpsertInstallation();
            installation.Uri = null;

            // Main
            try
            {
                _client = new NbSsePushReceiveClient();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Null URI", e.Message);
            }
        }

        /// <summary>
        /// Push通信開始 - インスタレーションが存在しない
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-7.5
        [Test]
        public void TestSendExceptionInstallationNotSaved()
        {
            try
            {
                _client = new NbSsePushReceiveClient();
                Assert.Fail("No Exception");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Null installationId", e.Message);
            }
        }

        /// <summary>
        /// Push通信開始 - ユーザ名が不正
        /// Close Callbackが返ること
        /// </summary>
        [Test]
        public async void TestConnectExceptionInvalidUsername()
        {
            // AutoRecovery()が実行されるので、Errorコールバックは起きない
            // Disconnect()が実行されるので、Closeコールバックが起きる

            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();
            installation.Username = "InvalidUsername";

            _client = new NbSsePushReceiveClient();

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Wait for Callback with Timeout
            manualEvent.WaitOne(10000);

            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: None
            Assert.AreEqual(0, _openCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);
        }

        /// <summary>
        /// Push通信開始 - パスワードが不正
        /// Close Callbackが返ること
        /// </summary>
        [Test]
        public async void TestConnectExceptionInvalidPassword()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();
            installation.Password = "InvalidPassword";

            _client = new NbSsePushReceiveClient();

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
                manualEvent.Set();
            });

            // Main
            _client.Connect();

            // Wait for Callback with Timeout
            manualEvent.WaitOne(10000);

            // Open Callback: None
            Assert.AreEqual(0, _openCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);
        }

        /// <summary>
        /// Push通信開始 - Uriが不正
        /// エラーが返ること
        /// </summary>
        /// NB-.NET-PUSH-7.5
        [Test]
        public async void TestConnectExceptionInvalidUri()
        {
            // For Callback Wait Class
            ManualResetEvent manualEvent = new ManualResetEvent(false);

            // Save Installation
            var installation = await ITUtil.UpsertInstallation();
            installation.Uri = TestConfig.SsePushEndpointUrl; // 404エラーになる(正常)

            _client = new NbSsePushReceiveClient();

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                try
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, statusCode);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
                manualEvent.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
            });

            // Main
            _client.Connect();

            // Wait for Callback with Timeout
            manualEvent.WaitOne(10000);

            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: None
            Assert.AreEqual(0, _openCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);

            // Error Callback: Once
            Assert.AreEqual(1, _errorCalledCount);
        }

        /// <summary>
        /// Push送信　- テナントID誤り
        /// エラーが返ること
        /// </summary>
        /// DOTNET-PUSH-1.5
        /// DOTNET-PUSH-1.5.1
        /// DOTNET-PUSH-1.5.1.1
        /// DOTNET-PUSH-1.5.1.2
        /// DOTNET-PUSH-1.6
        /// DOTNET-PUSH-1.6.2
        [Test]
        public async void TestSendExceptionInvalidTenantId()
        {
            var service = NbService.Singleton;
            service.TenantId = ("InvalidTenantId");

            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);
                Assert.IsNotNull(ITUtil.GetErrorInfo(e.Response));
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.TenantId;
            }
        }

        /// <summary>
        /// Push送信　- AppIdID誤り
        /// エラーが返ること
        /// </summary>
        /// DOTNET-PUSH-1.5
        /// DOTNET-PUSH-1.5.1
        /// DOTNET-PUSH-1.5.1.1
        /// DOTNET-PUSH-1.5.1.2
        /// DOTNET-PUSH-1.6
        /// DOTNET-PUSH-1.6.2
        [Test]
        public async void TestSendExceptionInvalidAppId()
        {
            var service = NbService.Singleton;
            service.AppId = ("InvalidAppId");

            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
                Assert.IsNotNull(ITUtil.GetErrorInfo(e.Response));
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.TenantId;
            }
        }

        /// <summary>
        /// Push送信　- AppKey誤り
        /// エラーが返ること
        /// </summary>
        /// DOTNET-PUSH-1.5
        /// DOTNET-PUSH-1.5.1
        /// DOTNET-PUSH-1.5.1.1
        /// DOTNET-PUSH-1.5.1.2
        /// DOTNET-PUSH-1.6
        /// DOTNET-PUSH-1.6.2
        [Test]
        public async void TestSendExceptionInvalidAppKey()
        {
            var service = NbService.Singleton;
            service.AppKey = ("InvalidAppKey");

            // Send Message
            var nebulaPush = new NbPush();

            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.Fail("No Exception");
            }
            catch (NbHttpException e)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, e.StatusCode);
                Assert.IsNotNull(ITUtil.GetErrorInfo(e.Response));
            }
            finally
            {
                // After Test
                service.TenantId = TestConfig.TenantId;
            }
        }

        /// <summary>
        /// Push通信終了 - 接続中状態
        /// 正しく通信終了すること
        /// </summary>
        // NB-.NET-PUSH-8.1
        // NB-.NET-SSE-2
        // NB-.NET-SSE-2.1
        [Test]
        public async void TestDisConnectNormal()
        {
            // For Open Callback Wait Class
            ManualResetEvent manualEventForOpen = new ManualResetEvent(false);
            // For Close Callback Wait Class
            ManualResetEvent manualEventForClose = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEventForOpen.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
                manualEventForClose.Set();
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
            });

            // Connect
            _client.Connect();

            manualEventForOpen.WaitOne(10000);

            // Main
            _client.Disconnect();

            manualEventForClose.WaitOne(10000);

            Assert.AreEqual(1, _openCalledCount);
            Assert.AreEqual(1, _closeCalledCount);
            Assert.AreEqual(0, _errorCalledCount);

            // 再度実行してもエラーが起きないこと
            try
            {
                _client.Disconnect();
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        /// <summary>
        /// Push通信開始・終了　繰り返し
        /// 正しく通信開始・終了すること
        /// </summary>
        [Test]
        public async void TestConnectAndDisConnectNormal()
        {
            // For Open Callback Wait Class
            ManualResetEvent manualEventForOpen = new ManualResetEvent(false);

            // For Close Callback Wait Class
            ManualResetEvent manualEventForClose = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEventForOpen.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
                manualEventForClose.Set();
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
            });

            // 3回繰り返し
            for (var i = 0; i < 3; i++)
            {
                // Connect
                _client.Connect();

                manualEventForOpen.WaitOne(10000);
                manualEventForOpen.Reset();

                // Main
                _client.Disconnect();

                manualEventForClose.WaitOne(10000);
                manualEventForClose.Reset();

                try
                {
                    Assert.AreEqual(i + 1, _openCalledCount);
                    Assert.AreEqual(i + 1, _closeCalledCount);
                    Assert.AreEqual(0, _errorCalledCount);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
            }

            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Three Times
            Assert.AreEqual(3, _openCalledCount);

            // Close Callback: Three Times
            Assert.AreEqual(3, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);
        }

        /// <summary>
        /// ネットワーク切断・接続
        /// SSE Pushサーバと接続中にネットワーク切断した場合、Push受信を終了すること
        /// 切断後、ネットワーク接続した場合、Push受信を開始すること。
        /// 本テストは手動で行うこと。すなわち、Wait中にNWを手動で切断・接続すること。
        /// テスト時は[Test]のコメントを外すこと。
        /// </summary>
        /// NB-.NET-PUSH-7.6 
        /// NB-.NET-PUSH-7.7 
        //[Test]
        public async void TestNwDisconnectAndConnect()
        {
            // For Open Callback Wait Class
            ManualResetEvent manualEventForOpen = new ManualResetEvent(false);

            // For Message Callback Wait Class
            ManualResetEvent manualEventForMessage = new ManualResetEvent(false);

            // For Message Callback Wait Class
            ManualResetEvent manualEventForClose = new ManualResetEvent(false);

            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback
            _client.RegisterOnMessage("TestEventType", (message) =>
            {
                _messageCalledCount++;

                try
                {
                    Assert.AreEqual("This is Test.", message.Data);
                    Assert.AreEqual("TestEventType", message.EventType);
                    Assert.AreEqual("TestId", message.LastEventId);
                    Assert.IsNull(message.Retry);
                }
                catch (Exception e)
                {
                    SetAssert(e.Message);
                }
                manualEventForMessage.Set();
            });

            // Register Open Callback
            _client.RegisterOnOpen(() =>
            {
                _openCalledCount++;
                manualEventForOpen.Set();
            });

            // Register Close Callback
            _client.RegisterOnClose(() =>
            {
                _closeCalledCount++;
                manualEventForClose.Set();
            });

            // Register Error Callback
            _client.RegisterOnError((statusCode, errorInfo) =>
            {
                _errorCalledCount++;
                SetAssert("Error CallBack");
            });

            // Main
            _client.Connect();

            // Wait for Callback With Timeout
            manualEventForOpen.WaitOne(10000);
            manualEventForOpen.Reset();

            // Wait for Close Callback With Timeout
            // Wait中に手動でネットワーク切断すること(1分以内)
            manualEventForClose.WaitOne(60000);

            // Wait中に手動でネットワーク接続すること(3分以内)
            // Wait for Callback With Timeout
            manualEventForOpen.WaitOne(180000);

            Thread.Sleep(2000);

            // Send Message
            var nebulaPush = new NbPush();
            nebulaPush.Query = new NbQuery().EqualTo("email", "ssetest@pushtest.com");
            nebulaPush.Message = "This is Test.";

            var sse = new NbSseFields();
            sse.EventId = "TestId";
            sse.EventType = "TestEventType";
            nebulaPush.SseFields = sse;

            try
            {
                var response = await nebulaPush.SendAsync();
                Assert.AreEqual("ok", response["result"]);
                Assert.AreEqual(1, response["installations"]);
            }
            catch (Exception)
            {
                SetAssert("Send Fail");
            }

            // Wait for Callback With Timeout
            manualEventForMessage.WaitOne(10000);

            // Test中にAssert.Fail()するとmanualEvent.Setが呼ばれずハングするので、最後にAssertionをthrowする
            if (_isAssertionExists)
            {
                ThrowAssert();
            }

            // Open Callback: Twice
            Assert.AreEqual(2, _openCalledCount);

            // Close Callback: Once
            Assert.AreEqual(1, _closeCalledCount);

            // Error Callback: None
            Assert.AreEqual(0, _errorCalledCount);

            // Message Callback: Once
            Assert.AreEqual(1, _messageCalledCount);
        }

        /// <summary>
        /// 受信コールバック登録
        /// 同じイベントタイプを2回登録した際、エラーとならないこと
        /// </summary>
        [Test]
        public async void TestRegisterOnMessageTwice()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Message Callback Twice
            _client.RegisterOnMessage("TestEventType", (message) => { });
            _client.RegisterOnMessage("TestEventType", (message) => { });
        }

        /// <summary>
        /// 接続コールバック登録
        /// 同じイベントタイプを2回登録した際、エラーとならないこと
        /// </summary>
        [Test]
        public async void TestRegisterOnOpenTwice()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Open Callback Twice
            _client.RegisterOnOpen(() => { });
            _client.RegisterOnOpen(() => { });
        }

        /// <summary>
        /// 切断コールバック登録
        /// 同じイベントタイプを2回登録した際、エラーとならないこと
        /// </summary>
        [Test]
        public async void TestRegisterOnCloseTwice()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Close Callback Twice
            _client.RegisterOnClose(() => { });
            _client.RegisterOnClose(() => { });
        }

        /// <summary>
        /// エラーコールバック登録
        /// 同じイベントタイプを2回登録した際、エラーとならないこと
        /// </summary>
        [Test]
        public async void TestRegisterOnErrorTwice()
        {
            // Save Installation
            await ITUtil.UpsertInstallation();

            _client = new NbSsePushReceiveClient();

            // Register Error Callback Twice
            _client.RegisterOnError((statusCode, errorInfo) => { });
            _client.RegisterOnError((statusCode, errorInfo) => { });
        }
    }
}
